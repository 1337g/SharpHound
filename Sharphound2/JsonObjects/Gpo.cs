﻿using System.Collections.Generic;

namespace Sharphound2.JsonObjects
{
    internal class Gpo : JsonBase
    {
        public string Name { get; set; }
        public string Guid { get; set; }

        public Dictionary<string, object> Properties = new Dictionary<string, object>();

        public ACL[] Aces { get; set; }
    }
}
