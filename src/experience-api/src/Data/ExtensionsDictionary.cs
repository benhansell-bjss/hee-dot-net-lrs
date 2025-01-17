﻿using Doctrina.ExperienceApi.Data.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Doctrina.ExperienceApi.Data
{
    public class ExtensionsDictionary : JsonModel, IDictionary<Uri, JToken>
    {
        private readonly IDictionary<Uri, JToken> _values;

        public ExtensionsDictionary() { }
        public ExtensionsDictionary(IEnumerable<KeyValuePair<Uri, JToken>> values)
        {
            _values = new Dictionary<Uri, JToken>();

            foreach (var value in values)
            {
                _values.Add(value);
            }
        }

        public ExtensionsDictionary(JToken jobj) : this(jobj, ApiVersion.GetLatest()) { }

        public ExtensionsDictionary(JToken extensions, ApiVersion version)
        {
            GuardType(extensions, JTokenType.Null, JTokenType.Object);

            if (extensions is JObject @object)
            {
                _values = new Dictionary<Uri, JToken>();

                var properties = @object.Properties();
                foreach (var token in properties)
                {
                    try
                    {
                        Add(new Uri(token.Name), token.Value);
                    }
                    catch (UriFormatException ex)
                    {
                        throw new JsonTokenModelException(token, ex.Message);
                    }
                }
            }
        }

        public int Count => _values.Count;

        public bool IsReadOnly => _values.IsReadOnly;

        public ICollection<Uri> Keys => _values.Keys;

        public ICollection<JToken> Values => _values.Values;

        public JToken this[Uri key] { get => _values[key]; set => _values[key] = value; }

        public void Add(Uri key, JToken value)
        {
            _values.Add(new KeyValuePair<Uri, JToken>(key, value));
        }

        public void Add(KeyValuePair<Uri, JToken> item)
        {
            _values.Add(item);
        }

        public void Clear()
        {
            _values.Clear();
        }

        public bool Contains(KeyValuePair<Uri, JToken> item)
        {
            return _values.Contains(item);
        }

        public void CopyTo(KeyValuePair<Uri, JToken>[] array, int arrayIndex)
        {
            _values.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<Uri, JToken> item)
        {
            return _values.Remove(item);
        }

        public IEnumerator<KeyValuePair<Uri, JToken>> GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        public override JToken ToJToken(ApiVersion version, ResultFormat format)
        {
            if (_values != null && _values.Count > 0)
            {
                var obj = new JObject();
                foreach (var val in _values)
                {
                    obj.Add(val.Key.ToString(), val.Value);
                }
                return obj;
            }

            return null;
        }

        public bool ContainsKey(Uri key)
        {
            return _values.ContainsKey(key);
        }

        public bool Remove(Uri key)
        {
            return _values.Remove(key);
        }

        public bool TryGetValue(Uri key, out JToken value)
        {
            return _values.TryGetValue(key, out value);
        }
    }
}
