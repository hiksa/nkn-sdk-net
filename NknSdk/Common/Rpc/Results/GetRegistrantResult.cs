﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Rpc.Results
{
    public class GetRegistrantResult
    {
        public string Registrant { get; set; }

        public int ExpiresAt { get; set; }
    }
}
