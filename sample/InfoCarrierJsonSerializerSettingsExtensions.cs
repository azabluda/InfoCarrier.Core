// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using Remote.Linq;

    public static class InfoCarrierJsonSerializerSettingsExtensions
    {
        public static JsonSerializerSettings ConfigureInfoCarrier(this JsonSerializerSettings jsonSerializerSettings)
        {
            jsonSerializerSettings = jsonSerializerSettings.ConfigureRemoteLinq();
            jsonSerializerSettings.SerializationBinder = new InfoCarrierJsonSerializationBinder();
            return jsonSerializerSettings;
        }

        private class InfoCarrierJsonSerializationBinder : DefaultSerializationBinder
        {
            // Don't export assembly name for system types, because it may slightly differ from platform to platform
            public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                base.BindToName(serializedType, out assemblyName, out typeName);
                if (assemblyName?.StartsWith("System.") == true)
                {
                    assemblyName = null;
                }
            }
        }
    }
}
