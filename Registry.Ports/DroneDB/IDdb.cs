﻿using System;
using System.Collections.Generic;
using System.Text;
using Registry.Ports.DroneDB.Models;

namespace Registry.Ports.DroneDB
{
    public interface IDdb
    {
        IEnumerable<DdbInfo> Info(string path);
    }
}
