using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Wallet
{
    public class ScryptParams
    {
        public int SaltLength { get; set; }

        public int N { get; set; }

        public int R { get; set; }

        public int P { get; set; }

        public string Salt { get; set; }
    }
}
