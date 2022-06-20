﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Valour.Shared.MPS
{
    public class MPSConfig
    {
        public static MPSConfig Current;

        public MPSConfig()
        {
            Current = this;
        }

        public string Api_Key { get; set; }

        public string Api_Key_Encoded { get; set; }
    }
}
