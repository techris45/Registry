﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DDB.Bindings;
using GeoJSON.Net;
using GeoJSON.Net.CoordinateReferenceSystem;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Registry.Common;
using Registry.Ports.DroneDB;
using Registry.Ports.DroneDB.Models;
using LineString = GeoJSON.Net.Geometry.LineString;
using Point = GeoJSON.Net.Geometry.Point;
using Polygon = GeoJSON.Net.Geometry.Polygon;

namespace Registry.Adapters.DroneDB
{

    public class Ddb : IDdb
    {
        private readonly string _baseDdbPath;

        public Ddb(string baseDdbPath)
        {

            _baseDdbPath = baseDdbPath;

            Directory.CreateDirectory(baseDdbPath);

            // TODO: It would be nice if we could use the bindings to check this
            if (!Directory.Exists(Path.Combine(baseDdbPath, ".ddb")))
            {
                try
                {
                    var res = DDB.Bindings.DroneDB.Init(baseDdbPath);
                    Debug.WriteLine(res);
                }
                catch (DDBException ex)
                {
                    throw new InvalidOperationException($"Cannot initialize ddb in folder '{baseDdbPath}'", ex);
                }
            }
        }

        static Ddb()
        {
#if DEBUG
            DDB.Bindings.DroneDB.RegisterProcess(true);
#else
            DDB.Bindings.DroneDB.RegisterProcess(false);
#endif
        }

        public IEnumerable<DdbEntry> Search(string path)
        {

            try
            {

                // If the path is not absolute let's rebase it on ddbPath
                if (path != null && !Path.IsPathRooted(path))
                    path = Path.Combine(_baseDdbPath, path);

                // If path is null we leverage the recursive parameter
                var info = path != null ?
                    DDB.Bindings.DroneDB.List(_baseDdbPath, path) :
                    DDB.Bindings.DroneDB.List(_baseDdbPath, _baseDdbPath, true);

                if (info == null)
                {
                    Debug.WriteLine("Strange null return value");
                    return new DdbEntry[0];
                }

                var query = from item in info
                            select new DdbEntry
                            {
                                Depth = item.Depth,
                                Hash = item.Hash,
                                Meta = item.Meta,
                                ModifiedTime = item.ModifiedTime,
                                Path = item.Path,
                                Size = item.Size,
                                Type = (Common.EntryType)(int)item.Type,

                                PointGeometry = (Point)item.PointGeometry?.ToObject<Feature>()?.Geometry,
                                PolygonGeometry = (Polygon)item.PolygonGeometry?.ToObject<Feature>()?.Geometry
                            };


                return query.ToArray();
            }
            catch (DDBException ex)
            {
                throw new InvalidOperationException($"Cannot list '{path}' to ddb '{_baseDdbPath}'", ex);
            }


        }

        public void Add(string path, byte[] data)
        {
            using var stream = new MemoryStream(data);
            Add(path, stream);
        }

        public void Remove(string path)
        {
            try
            {

                // If the path is not absolute let's rebase it on ddbPath
                if (!Path.IsPathRooted(path)) path = Path.Combine(_baseDdbPath, path);

                DDB.Bindings.DroneDB.Remove(_baseDdbPath, path);
            }
            catch (DDBException ex)
            {
                throw new InvalidOperationException($"Cannot remove '{path}' from ddb '{_baseDdbPath}'", ex);
            }
        }

        public Dictionary<string, object> ChangeAttributes(Dictionary<string, object> attributes)
        {
            try
            {
                
                return DDB.Bindings.DroneDB.ChangeAttributes(_baseDdbPath, attributes);

            }
            catch (DDBException ex)
            {
                throw new InvalidOperationException($"Cannot change attributes of ddb '{_baseDdbPath}'", ex);
            }
        }

        public void GenerateThumbnail(string imagePath, int size, string outputPath)
        {
            try
            {

                DDB.Bindings.DroneDB.GenerateThumbnail(imagePath, size, outputPath);

            }
            catch (DDBException ex)
            {
                throw new InvalidOperationException($"Cannot generate thumbnail of '{imagePath}' to '{outputPath}' with size '{size}'", ex);
            }
        }

        public void Add(string path, Stream stream)
        {
            string filePath = null;

            try
            {

                filePath = Path.Combine(_baseDdbPath, path);
                
                stream.Reset();

                EnsureFolderExists(filePath);

                using (var writer = File.OpenWrite(filePath))
                {
                    stream.CopyTo(writer);
                }

                DDB.Bindings.DroneDB.Add(_baseDdbPath, filePath);

            }
            catch (DDBException ex)
            {
                throw new InvalidOperationException($"Cannot add '{path}' to ddb '{_baseDdbPath}'", ex);
            }
            finally
            {
                if (filePath != null && File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        private static void EnsureFolderExists(string filePath)
        {
            var folder = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(folder);
        }
    }
}
