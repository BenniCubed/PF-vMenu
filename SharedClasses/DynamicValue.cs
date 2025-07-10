using System;

using Newtonsoft.Json;

namespace vMenuShared
{
    public enum DynamicValueType
    {
        String,
        Float,
        Int,
        Bool
    }

    public class DynamicValue : IEquatable<DynamicValue>
    {
        [JsonProperty]
        public DynamicValueType Type { get; private set; }

        public DynamicValue(string value) : this(value, DynamicValueType.String) { }
        public DynamicValue(float value) : this(value, DynamicValueType.Float) { }
        public DynamicValue(int value) : this(value, DynamicValueType.Int) { }
        public DynamicValue(bool value) : this(value, DynamicValueType.Bool) { }

        public static DynamicValue FromObject(object obj, DynamicValueType? expectedType = null)
        {
            var value = obj switch
            {
                string str => new DynamicValue(str),
                float f => new DynamicValue(f),
                int i => new DynamicValue(i),
                bool b => new DynamicValue(b),
                _ => throw new ArgumentException($"Unsupported type: {obj.GetType()}")
            };

            if (expectedType.HasValue && value.Type != expectedType.Value)
            {
                throw new InvalidCastException($"Expected type {expectedType.Value}, but got {value.Type}.");
            }

            return value;
        }

        public string AsString() => AsT<string>();
        public float AsFloat() => AsT<float>();
        public int AsInt() => AsT<int>();
        public bool AsBool() => AsT<bool>();
        public object AsObject() => Value;

        public override bool Equals(object obj)
        {
            return Equals(obj as DynamicValue);
        }

        public bool Equals(DynamicValue other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Type == other.Type && Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return new Tuple<DynamicValueType, object>(Type, Value).GetHashCode();
        }

        public static bool operator ==(DynamicValue left, DynamicValue right)
        {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(DynamicValue left, DynamicValue right)
        {
            return !(left == right);
        }

        [JsonConstructor]
        private DynamicValue(object value, DynamicValueType type)
        {
            Value = value;
            Type = type;
        }

        private T AsT<T>()
        {
            if (Value is T value)
            {
                return value;
            }
            throw new InvalidCastException($"Cannot cast {Value.GetType()} to {typeof(T)}");
        }

        [JsonProperty]
        private object Value { get; set; }
    }
}
