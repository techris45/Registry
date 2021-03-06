﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Registry.Ports.DroneDB;

namespace Registry.Web.Services.Ports
{
    /// <summary>
    /// Creates new instances of IDdb
    /// </summary>
    public interface IDdbFactory
    {
        IDdb GetDdb(string orgSlug, string dsSlug);
    }
}
