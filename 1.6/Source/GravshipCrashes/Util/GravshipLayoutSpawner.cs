using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipCrashes.Util
{
    /// <summary>
    /// Attempts to place ship layouts exported by the Gravship Exporter mod.
    /// </summary>
    public static class GravshipLayoutSpawner
    {
        private static readonly Dictionary<Type, MethodInfo> PlacementMethods = new();

        public static bool TrySpawnLayout(Map map, ShipLayoutResolver.ShipEntry entry, IntVec3 center, List<Thing> placedThings)
        {
            if (map == null || entry == null)
            {
                return false;
            }

            var type = entry.RawDef?.GetType();
            if (type == null)
            {
                return false;
            }

            if (!PlacementMethods.TryGetValue(type, out var placementMethod))
            {
                placementMethod = ResolvePlacementMethod(type);
                PlacementMethods[type] = placementMethod;
            }

            if (placementMethod != null)
            {
                try
                {
                    var parameters = placementMethod.GetParameters();
                    var args = BuildArguments(parameters, map, center, entry.RawDef);
                    if (args != null)
                    {
                        var result = placementMethod.Invoke(entry.RawDef, args);
                        if (result is bool success && success)
                        {
                            CollectPlacedThings(map, placedThings, center, 50);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[GravshipCrashes] Failed to invoke ShipLayout placement method: " + ex);
                }
            }

            return false;
        }

        private static MethodInfo ResolvePlacementMethod(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.ReturnType != typeof(bool))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length < 2)
                {
                    continue;
                }

                if (parameters[0].ParameterType != typeof(Map))
                {
                    continue;
                }

                return method;
            }

            return null;
        }

        private static object[] BuildArguments(ParameterInfo[] parameters, Map map, IntVec3 center, object def)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return null;
            }

            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var type = parameters[i].ParameterType;
                if (type == typeof(Map))
                {
                    args[i] = map;
                }
                else if (type == typeof(IntVec3))
                {
                    args[i] = center;
                }
                else if (type == typeof(Rot4))
                {
                    args[i] = Rot4.North;
                }
                else if (type == typeof(bool))
                {
                    args[i] = true;
                }
                else if (type == typeof(ThingDef))
                {
                    args[i] = ThingDefOf.ShipChunk;
                }
                else if (type == def?.GetType())
                {
                    args[i] = def;
                }
                else
                {
                    args[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                }
            }

            return args;
        }

        public static CellRect CalculateBounds(List<Thing> placedThings, Map map)
        {
            if (placedThings == null || placedThings.Count == 0)
            {
                return CellRect.CenteredOn(map.Center, 20, 20);
            }

            var min = new IntVec3(int.MaxValue, 0, int.MaxValue);
            var max = new IntVec3(int.MinValue, 0, int.MinValue);

            foreach (var thing in placedThings)
            {
                if (thing == null)
                {
                    continue;
                }

                min.x = Mathf.Min(min.x, thing.Position.x);
                min.z = Mathf.Min(min.z, thing.Position.z);
                max.x = Mathf.Max(max.x, thing.Position.x);
                max.z = Mathf.Max(max.z, thing.Position.z);
            }

            var width = Mathf.Max(10, max.x - min.x + 10);
            var height = Mathf.Max(10, max.z - min.z + 10);

            return CellRect.CenteredOn(map.Center, width, height).ClipInsideMap(map);
        }

        private static void CollectPlacedThings(Map map, List<Thing> results, IntVec3 center, int radius)
        {
            if (results == null)
            {
                return;
            }

            var cells = GenRadial.RadialCellsAround(center, radius, true);
            foreach (var cell in cells)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                results.AddRange(cell.GetThingList(map));
            }
        }
    }
}
