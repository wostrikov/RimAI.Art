/*
 * Purpose:
 * - Safely access RimTalk Constant values without hard dependency on its members.
 *
 * Notes:
 * - Falls back to local defaults when Ustas.RimAI.Communication.Data.Constant throws.
 */
using System;
using System.Reflection;
using Verse;

namespace Ustas.RimAI.Art
{
    public static class RimTalkConstantShim
    {
        private const string DefaultCloudModelFallback = "gemma-3-27b-it";
        private const string FallbackCloudModelFallback = "gemma-3-12b-it";
        private const string ChooseModelFallback = "(choose model)";
        private static readonly Type ConstantType = FindConstantType();

        private static Type FindConstantType()
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType("Ustas.RimAI.Communication.Data.Constant", false);
                    if (type != null) return type;
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        private static string GetConstantString(string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName)) return null;
            if (ConstantType == null) return null;

            try
            {
                var prop = ConstantType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
                if (prop != null && prop.PropertyType == typeof(string))
                    return prop.GetValue(null, null) as string;

                var field = ConstantType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
                if (field != null && field.FieldType == typeof(string))
                    return field.GetValue(null) as string;
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        public static string Lang
        {
            get
            {
                return GetConstantString("Lang") ??
                    (LanguageDatabase.activeLanguage?.info?.friendlyNameNative ?? "English");
            }
        }

        public static string DefaultCloudModel
        {
            get
            {
                return GetConstantString("DefaultCloudModel") ?? DefaultCloudModelFallback;
            }
        }

        public static string FallbackCloudModel
        {
            get
            {
                return GetConstantString("FallbackCloudModel") ?? FallbackCloudModelFallback;
            }
        }

        public static string ChooseModel
        {
            get
            {
                return GetConstantString("ChooseModel") ?? ChooseModelFallback;
            }
        }
    }
}
