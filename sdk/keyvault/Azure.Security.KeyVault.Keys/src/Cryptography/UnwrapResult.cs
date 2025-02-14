﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Security.KeyVault.Keys.Cryptography
{
    /// <summary>
    /// Represents information about an unwrap operation
    /// </summary>
    public class UnwrapResult : IJsonDeserializable
    {
        private const string KeyIdPropertyName = "kid";
        private const string KeyPropertyName = "value";

        internal UnwrapResult()
        {
        }

        /// <summary>
        /// The <see cref="KeyProperties.Id"/> of the <see cref="Key"/> used to uwrap
        /// </summary>
        public string KeyId { get; internal set; }

        /// <summary>
        /// The unwrapped key
        /// </summary>
        public byte[] Key { get; internal set; }

        /// <summary>
        /// The algorithm used
        /// </summary>
        public KeyWrapAlgorithm Algorithm { get; internal set; }

        void IJsonDeserializable.ReadProperties(JsonElement json)
        {
            foreach (JsonProperty prop in json.EnumerateObject())
            {
                switch (prop.Name)
                {
                    case KeyIdPropertyName:
                        KeyId = prop.Value.GetString();
                        break;
                    case KeyPropertyName:
                        Key = Base64Url.Decode(prop.Value.GetString());
                        break;
                }
            }
        }
    }
}
