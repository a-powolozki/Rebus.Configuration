using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Rebus.Configuration.Settings
{
    class MethodInvocationInformation : IDictionary<string, object>
    {
        public string MethodName { get; }

        public IDictionary<string, object> Parameters { get; }

        public IEnumerable<MethodInvocationInformation> SubsequentConfigurations { get; }

        #region IDictionary<string, object> implementation

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            return Parameters.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Parameters.GetEnumerator();
        }

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
        {
            Parameters.Add(item);
        }

        void ICollection<KeyValuePair<string, object>>.Clear()
        {
            Parameters.Clear();
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            return Parameters.Contains(item);
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            Parameters.CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            return Parameters.Remove(item);
        }

        int ICollection<KeyValuePair<string, object>>.Count => Parameters.Count;

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly => Parameters.IsReadOnly;

        void IDictionary<string, object>.Add(string key, object value)
        {
            Parameters.Add(key, value);
        }

        bool IDictionary<string, object>.ContainsKey(string key)
        {
            return Parameters.ContainsKey(key);
        }

        bool IDictionary<string, object>.Remove(string key)
        {
            return Parameters.Remove(key);
        }

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            return Parameters.TryGetValue(key, out value);
        }

        object IDictionary<string, object>.this[string key]
        {
            get => Parameters[key];
            set => Parameters[key] = value;
        }

        ICollection<string> IDictionary<string, object>.Keys => Parameters.Keys;

        ICollection<object> IDictionary<string, object>.Values => Parameters.Values;

        #endregion

        public MethodInvocationInformation(string methodName) : this(methodName, new Dictionary<string, object>()) { }

        public MethodInvocationInformation(string methodName, IDictionary<string, object> parameters) : this(methodName, parameters, Enumerable.Empty<MethodInvocationInformation>()) { }
        public MethodInvocationInformation(string methodName, IDictionary<string, object> parameters, IEnumerable<MethodInvocationInformation> subsequentConfigurations)
        {
            MethodName = methodName;
            Parameters = new ConcurrentDictionary<string, object>(parameters);
            SubsequentConfigurations = subsequentConfigurations;
        }
    }
}