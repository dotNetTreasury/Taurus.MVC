﻿
using Microsoft.AspNetCore.Http;

namespace Taurus.Plugin.Admin
{
    internal static class KestrelExtenstions
    {
        public static void RefleshOptions()
        {
            TaurusExtensions.RefleshOptions();
        }
    }
}
