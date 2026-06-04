using System;
using System.Collections.Generic;
using System.Reflection;
using TCAMultiplayer.Core;

namespace TCAMultiplayer.Game
{
    /// <summary>
    /// Helper for querying game map data without taking hard dependencies on internal data-store types.
    /// </summary>
    public static class MapHelper
    {
        private const string Tag = "MAP-HELPER";

        private static bool _initialized;
        private static Type _gameDataMapsType;
        private static Type _gameDataType;
        private static Type _mapDataType;
        private static MethodInfo _getDefaultMapNameMethod;
        private static MethodInfo _getMapDisplayNameMethod;
        private static MethodInfo _getByNameMethod;
        private static FieldInfo _gameDataCoreField;
        private static FieldInfo _coreMapsField;
        private static FieldInfo _mapsDataField;
        private static FieldInfo _mapNameField;
        private static FieldInfo _mapDisplayNameField;
        private static FieldInfo _mapSelectableField;
        private static FieldInfo _mapDefaultAirfieldField;
        private static FieldInfo _mapAirfieldsField;
        private static FieldInfo _strategicNameField;
        private static FieldInfo _strategicDisplayNameField;

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                _gameDataMapsType = Type.GetType("Falcon.GameDataMaps, Assembly-CSharp");
                _gameDataType = Type.GetType("Falcon.GameData, Assembly-CSharp");

                _getDefaultMapNameMethod = _gameDataMapsType?.GetMethod("GetDefaultMapName", BindingFlags.Public | BindingFlags.Static);
                _getMapDisplayNameMethod = _gameDataMapsType?.GetMethod("GetMapDisplayName", BindingFlags.Public | BindingFlags.Static);
                _getByNameMethod = _gameDataMapsType?.GetMethod("GetByName", BindingFlags.Public | BindingFlags.Static);

                _gameDataCoreField = _gameDataType?.GetField("Core", BindingFlags.Public | BindingFlags.Static);

                if (_getByNameMethod != null)
                {
                    _mapDataType = _getByNameMethod.ReturnType;
                    _mapNameField = _mapDataType.GetField("Name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    _mapDisplayNameField = _mapDataType.GetField("DisplayName", BindingFlags.Public | BindingFlags.Instance);
                    _mapSelectableField = _mapDataType.GetField("IsSelectable", BindingFlags.Public | BindingFlags.Instance);
                    _mapDefaultAirfieldField = _mapDataType.GetField("DefaultAirfield", BindingFlags.Public | BindingFlags.Instance);
                    _mapAirfieldsField = _mapDataType.GetField("Airfields", BindingFlags.Public | BindingFlags.Instance);

                    var airfieldsType = _mapAirfieldsField?.FieldType;
                    if (airfieldsType != null && airfieldsType.IsGenericType)
                    {
                        var strategicType = airfieldsType.GetGenericArguments()[0];
                        _strategicNameField = strategicType.GetField("Name", BindingFlags.Public | BindingFlags.Instance);
                        _strategicDisplayNameField = strategicType.GetField("DisplayName", BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                var core = _gameDataCoreField?.GetValue(null);
                if (core != null)
                {
                    _coreMapsField = core.GetType().GetField("Maps", BindingFlags.Public | BindingFlags.Instance);
                    var mapsStore = _coreMapsField?.GetValue(core);
                    _mapsDataField = mapsStore?.GetType().GetField("Data", BindingFlags.Public | BindingFlags.Instance);
                }

                _initialized = true;
                Log.Info(Tag, "Initialized");
            }
            catch (Exception ex)
            {
                Log.Warning(Tag, $"Initialize failed: {ex.Message}");
            }
        }

        public static string GetDefaultMapName()
        {
            if (!_initialized) Initialize();
            try
            {
                var result = _getDefaultMapNameMethod?.Invoke(null, null) as string;
                return string.IsNullOrEmpty(result) ? "ActionIsland" : result;
            }
            catch
            {
                return "ActionIsland";
            }
        }

        public static string GetMapDisplayName(string mapName)
        {
            if (string.IsNullOrEmpty(mapName)) return mapName;
            if (!_initialized) Initialize();
            try
            {
                var result = _getMapDisplayNameMethod?.Invoke(null, new object[] { mapName }) as string;
                return string.IsNullOrEmpty(result) ? mapName : result;
            }
            catch
            {
                return mapName;
            }
        }

        public static List<string> GetSelectableMapNames()
        {
            if (!_initialized) Initialize();

            var names = new List<string>();
            try
            {
                var core = _gameDataCoreField?.GetValue(null);
                var mapsStore = core != null ? _coreMapsField?.GetValue(core) : null;
                var dataObj = mapsStore != null ? _mapsDataField?.GetValue(mapsStore) : null;
                if (dataObj is System.Collections.IDictionary dict)
                {
                    foreach (var value in dict.Values)
                    {
                        if (value == null) continue;
                        bool selectable = true;
                        if (_mapSelectableField != null)
                            selectable = _mapSelectableField.GetValue(value) is bool b && b;
                        if (!selectable) continue;

                        var name = _mapNameField?.GetValue(value) as string;
                        if (!string.IsNullOrEmpty(name))
                            names.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(Tag, $"GetSelectableMapNames failed: {ex.Message}");
            }

            if (names.Count == 0)
                names.Add(GetDefaultMapName());
            return names;
        }

        public static List<string> GetAirfieldNames(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                mapName = GetDefaultMapName();
            if (!_initialized) Initialize();

            var names = new List<string>();
            try
            {
                var mapData = _getByNameMethod?.Invoke(null, new object[] { mapName });
                var airfields = mapData != null ? _mapAirfieldsField?.GetValue(mapData) : null;
                if (airfields is System.Collections.IEnumerable enumerable)
                {
                    foreach (var airfield in enumerable)
                    {
                        var name = _strategicNameField?.GetValue(airfield) as string;
                        if (!string.IsNullOrEmpty(name))
                            names.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(Tag, $"GetAirfieldNames failed for '{mapName}': {ex.Message}");
            }

            return names;
        }

        public static string GetDefaultAirfieldName(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                mapName = GetDefaultMapName();
            if (!_initialized) Initialize();

            try
            {
                var mapData = _getByNameMethod?.Invoke(null, new object[] { mapName });
                var defaultAirfield = _mapDefaultAirfieldField?.GetValue(mapData) as string;
                if (!string.IsNullOrEmpty(defaultAirfield))
                    return defaultAirfield;

                var airfields = mapData != null ? _mapAirfieldsField?.GetValue(mapData) : null;
                if (airfields is System.Collections.IEnumerable enumerable)
                {
                    foreach (var airfield in enumerable)
                    {
                        var name = _strategicNameField?.GetValue(airfield) as string;
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetAirfieldDisplayName(string mapName, string airfieldName)
        {
            if (string.IsNullOrEmpty(airfieldName)) return airfieldName;
            if (string.IsNullOrEmpty(mapName))
                mapName = GetDefaultMapName();
            if (!_initialized) Initialize();

            try
            {
                var mapData = _getByNameMethod?.Invoke(null, new object[] { mapName });
                var airfields = mapData != null ? _mapAirfieldsField?.GetValue(mapData) : null;
                if (airfields is System.Collections.IEnumerable enumerable)
                {
                    foreach (var airfield in enumerable)
                    {
                        var name = _strategicNameField?.GetValue(airfield) as string;
                        if (!string.Equals(name, airfieldName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var display = _strategicDisplayNameField?.GetValue(airfield) as string;
                        return string.IsNullOrEmpty(display) ? airfieldName : display;
                    }
                }
            }
            catch
            {
                // Fall through to raw name.
            }

            return airfieldName;
        }

        public static bool IsAirfieldOnMap(string mapName, string airfieldName)
        {
            if (string.IsNullOrEmpty(airfieldName)) return false;
            foreach (var name in GetAirfieldNames(mapName))
                if (string.Equals(name, airfieldName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
