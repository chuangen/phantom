﻿// Coded by chuangen http://chuangen.name.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elton.Phantom
{
    /// <summary>
    /// URL参数。
    /// </summary>
    internal class UrlSegment
    {
        public string Key { get; private set; }
        public string Value { get; private set; }
        public UrlSegment(string key, string value)
        {
            this.Key = key;
            this.Value = value;
        }
    }
}
