using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CloudWhale.Game
{
    public interface IStateStorage
    {
        bool TryRead(out string value, out string reason);
        bool TryWrite(string value, out string reason);
    }

    public static class GameStateSerializer
    {
        public static string Serialize(GameStateData data) => JsonUtility.ToJson(data);

        public static bool TryDeserialize(string value, out GameStateData data)
        {
            if (!HasCompleteCurrentState(value))
            {
                data = null;
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<GameStateData>(value);
                if (data != null) data.NormalizeExtensions();
                return data != null;
            }
            catch (ArgumentException)
            {
                data = null;
                return false;
            }
        }

        // JsonUtility silently supplies CLR defaults for omitted or mismatched fields. Validate the
        // persisted state fields first so a truncated save cannot be treated as a real zero-resource save.
        private static bool HasCompleteCurrentState(string value)
        {
            if (!TryReadTopLevelNumberFields(value, out var fields)) return false;

            return HasInt(fields, "version")
                && HasLong(fields, "savedAtUnixSeconds")
                && HasInt(fields, "driftwood")
                && HasInt(fields, "cloudCotton")
                && HasInt(fields, "dew")
                && HasInt(fields, "stardust")
                && HasInt(fields, "houseStage");
        }

        private static bool HasInt(IReadOnlyDictionary<string, string> fields, string name)
        {
            return fields.TryGetValue(name, out var value)
                && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
        }

        private static bool HasLong(IReadOnlyDictionary<string, string> fields, string name)
        {
            return fields.TryGetValue(name, out var value)
                && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
        }

        private static bool TryReadTopLevelNumberFields(string json, out Dictionary<string, string> fields)
        {
            fields = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(json)) return false;

            var index = 0;
            SkipWhitespace(json, ref index);
            if (!Consume(json, ref index, '{')) return false;
            SkipWhitespace(json, ref index);
            if (Consume(json, ref index, '}'))
            {
                SkipWhitespace(json, ref index);
                return index == json.Length;
            }

            while (true)
            {
                if (!TryReadString(json, ref index, out var name)) return false;
                SkipWhitespace(json, ref index);
                if (!Consume(json, ref index, ':')) return false;
                SkipWhitespace(json, ref index);
                if (!TryReadValue(json, ref index, out var numericValue)) return false;
                if (fields.ContainsKey(name)) return false;
                fields.Add(name, numericValue);
                SkipWhitespace(json, ref index);

                if (Consume(json, ref index, '}'))
                {
                    SkipWhitespace(json, ref index);
                    return index == json.Length;
                }

                if (!Consume(json, ref index, ',')) return false;
                SkipWhitespace(json, ref index);
            }
        }

        private static bool TryReadValue(string json, ref int index, out string numericValue)
        {
            numericValue = null;
            if (index >= json.Length) return false;

            if (json[index] == '"') return TryReadString(json, ref index, out _);
            if (json[index] == '{' || json[index] == '[') return SkipCompoundValue(json, ref index);
            if (json[index] == 't') return ConsumeLiteral(json, ref index, "true");
            if (json[index] == 'f') return ConsumeLiteral(json, ref index, "false");
            if (json[index] == 'n') return ConsumeLiteral(json, ref index, "null");

            var start = index;
            if (!TryReadJsonNumber(json, ref index)) return false;
            numericValue = json.Substring(start, index - start);
            return true;
        }

        private static bool SkipCompoundValue(string json, ref int index)
        {
            var opening = json[index++];
            var closing = opening == '{' ? '}' : ']';
            var depth = 1;
            while (index < json.Length && depth > 0)
            {
                if (json[index] == '"')
                {
                    if (!TryReadString(json, ref index, out _)) return false;
                    continue;
                }

                if (json[index] == opening) depth++;
                else if (json[index] == closing) depth--;
                index++;
            }

            return depth == 0;
        }

        private static bool TryReadString(string json, ref int index, out string value)
        {
            value = null;
            if (!Consume(json, ref index, '"')) return false;
            var start = index;
            var escaped = false;
            while (index < json.Length)
            {
                var character = json[index++];
                if (escaped) { escaped = false; continue; }
                if (character == '\\') { escaped = true; continue; }
                if (character == '"')
                {
                    value = json.Substring(start, index - start - 1);
                    return true;
                }

                if (character < ' ') return false;
            }

            return false;
        }

        private static bool TryReadJsonNumber(string json, ref int index)
        {
            Consume(json, ref index, '-');
            if (index >= json.Length) return false;

            if (json[index] == '0') index++;
            else if (json[index] >= '1' && json[index] <= '9')
            {
                do { index++; } while (index < json.Length && char.IsDigit(json[index]));
            }
            else return false;

            if (Consume(json, ref index, '.'))
            {
                var fractionStart = index;
                while (index < json.Length && char.IsDigit(json[index])) index++;
                if (index == fractionStart) return false;
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-')) index++;
                var exponentStart = index;
                while (index < json.Length && char.IsDigit(json[index])) index++;
                if (index == exponentStart) return false;
            }

            return true;
        }

        private static bool ConsumeLiteral(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length || json.Substring(index, literal.Length) != literal) return false;
            index += literal.Length;
            return true;
        }

        private static bool Consume(string text, ref int index, char character)
        {
            if (index >= text.Length || text[index] != character) return false;
            index++;
            return true;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        }
    }

    public static class GameStorageFactory
    {
        public static IStateStorage CreateDefault() => new BrowserLocalStorage("cloudwhale-island.state.v1");
    }

    public sealed class BrowserLocalStorage : IStateStorage
    {
        private readonly string key;
        private readonly IBrowserLocalStorageBridge bridge;

        public BrowserLocalStorage(string key) : this(key, new UnityWebLocalStorageBridge()) { }

        public BrowserLocalStorage(string key, IBrowserLocalStorageBridge bridge)
        {
            this.key = key ?? throw new ArgumentNullException(nameof(key));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool TryRead(out string value, out string reason)
        {
            return bridge.TryRead(key, out value, out reason);
        }

        public bool TryWrite(string value, out string reason)
        {
            return bridge.TryWrite(key, value, out reason);
        }
    }

    public interface IBrowserLocalStorageBridge
    {
        // A successful null value means the key is absent. Failures must return false with a reason.
        bool TryRead(string key, out string value, out string reason);
        bool TryWrite(string key, string value, out string reason);
    }

    public sealed class UnityWebLocalStorageBridge : IBrowserLocalStorageBridge
    {
        public bool TryRead(string key, out string value, out string reason)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                value = CloudWhaleLocalStorageGet(key);
                if (CloudWhaleLocalStorageDidLastReadFail() == 1)
                {
                    reason = "Browser local storage could not be read.";
                    return false;
                }

                reason = null;
                return true;
            }
            catch (Exception exception) { value = null; reason = exception.Message; return false; }
#else
            value = null;
            reason = "Browser local storage is only available in a Unity Web player.";
            return false;
#endif
        }

        public bool TryWrite(string key, string value, out string reason)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                if (CloudWhaleLocalStorageSet(key, value) == 1) { reason = null; return true; }
                reason = "Browser local storage rejected the write.";
                return false;
            }
            catch (Exception exception) { reason = exception.Message; return false; }
#else
            reason = "Browser local storage is only available in a Unity Web player.";
            return false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string CloudWhaleLocalStorageGet(string key);
        [DllImport("__Internal")] private static extern int CloudWhaleLocalStorageDidLastReadFail();
        [DllImport("__Internal")] private static extern int CloudWhaleLocalStorageSet(string key, string value);
#endif
    }
}
