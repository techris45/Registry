﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Registry.Web.Models.DTO;

namespace Registry.Web.Services.Ports
{
    public interface IShareManager
    {
        public Task<string> Initialize(ShareInitDto parameters);
        public Task<UploadResultDto> Upload(string token, string path, byte[] data);
        public Task<CommitResultDto> Commit(string token);
        Task<IEnumerable<BatchDto>> ListBatches(string orgSlug, string dsSlug);
    }
}