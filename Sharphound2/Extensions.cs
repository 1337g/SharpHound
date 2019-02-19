﻿using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Security.Principal;
using System.Text;
using Newtonsoft.Json;
using Sharphound2.Enumeration;

namespace Sharphound2
{
    public static class Extensions
    {
        private static readonly HashSet<string> Groups = new HashSet<string> { "268435456", "268435457", "536870912", "536870913" };
        private static readonly HashSet<string> Computers = new HashSet<string> { "805306369" };
        private static readonly HashSet<string> Users = new HashSet<string> { "805306368" };
        //private static readonly Regex SpnSearch = new Regex(@"HOST\/([A-Za-z0-9-_]*\.[A-Za-z0-9-_]*\.[A-Za-z0-9-_]*)$", RegexOptions.Compiled);
        private static string _primaryDomain;

        internal static void SetPrimaryDomain(string domain)
        {
            _primaryDomain = domain;
        }

        public static string ToTitleCase(this string str)
        {
            return str.Substring(0, 1).ToUpper() + str.Substring(1).ToLower();
        }

        internal static void PrintEntry(this SearchResultEntry result)
        {
            foreach (var property in result.Attributes.AttributeNames)
            {
                Console.WriteLine(property);
            }
        }

        internal static void CloseC(this JsonTextWriter writer, int count, string type)
        {
            writer.WriteEndArray();
            writer.WritePropertyName("meta");
            writer.WriteStartObject();
            writer.WritePropertyName("count");
            writer.WriteValue(count);
            writer.WritePropertyName("type");
            writer.WriteValue(type);
            writer.WriteEndObject();
            writer.Close();
        }

        internal static ResolvedEntry ResolveAdEntry(this SearchResultEntry result)
        {
            var entry = new ResolvedEntry();

            var accountName = result.GetProp("samaccountname");
            var distinguishedName = result.DistinguishedName;
            var accountType = result.GetProp("samaccounttype");

            if (distinguishedName == null)
                return null;

            var domainName = Utils.ConvertDnToDomain(distinguishedName);

            if (Groups.Contains(accountType))
            {
                entry.BloodHoundDisplay = $"{accountName}@{domainName}".ToUpper();
                entry.ObjectType = "group";
                return entry;
            }

            if (Users.Contains(accountType))
            {
                entry.BloodHoundDisplay = $"{accountName}@{domainName}".ToUpper();
                entry.ObjectType = "user";
                return entry;
            }

            if (Computers.Contains(accountType))
            {
                var shortName = accountName?.TrimEnd('$');
                var dnshostname = result.GetProp("dnshostname");
                
                if (dnshostname == null)
                {
                    bool hostFound;
                    if (domainName.Equals(_primaryDomain, StringComparison.CurrentCultureIgnoreCase))
                    {
                        hostFound = DnsManager.HostExistsDns(shortName, out dnshostname);
                        if (!hostFound)
                            hostFound = DnsManager.HostExistsDns($"{shortName}.{domainName}", out dnshostname);
                    }
                    else
                    {
                        hostFound = DnsManager.HostExistsDns($"{shortName}.{domainName}", out dnshostname);
                        if (!hostFound)
                            hostFound = DnsManager.HostExistsDns(shortName, out dnshostname);
                    }

                    if (!hostFound)
                        return null;
                    
                }
                entry.BloodHoundDisplay = dnshostname;
                entry.ObjectType = "computer";
                entry.ComputerSamAccountName = shortName;
                return entry;
            }

            

            if (accountType == null)
            {
                var objClass = result.GetPropArray("objectClass");
                if (objClass.Contains("groupPolicyContainer"))
                {
                    entry.BloodHoundDisplay = $"{result.GetProp("displayname")}@{domainName}";
                    entry.ObjectType = "gpo";
                    return entry;
                }

                if (objClass.Contains("organizationalUnit"))
                {
                    entry.BloodHoundDisplay = $"{result.GetProp("name")}@{domainName}";
                    entry.ObjectType = "ou";
                    return entry;
                }

                if (objClass.Contains("container"))
                {
                    entry.BloodHoundDisplay = domainName;
                    entry.ObjectType = "container";
                    return entry;
                }
            }
            else
            {
                if (accountType.Equals("805306370"))
                    return null;
            }
            entry.BloodHoundDisplay = domainName;
            entry.ObjectType = "domain";
            return entry;
        }

        public static string GetProp(this SearchResultEntry result, string prop)
        {
            if (!result.Attributes.Contains(prop))
                return null;

            return result.Attributes[prop][0].ToString();
        }

        public static byte[] GetPropBytes(this SearchResultEntry result, string prop)
        {
            if (!result.Attributes.Contains(prop))
                return null;

            return result.Attributes[prop][0] as byte[];
        }

        public static string[] GetPropArray(this SearchResultEntry result, string prop)
        {
            if (!result.Attributes.Contains(prop))
                return new string[0];

            var values = result.Attributes[prop];

            var toreturn = new string[values.Count];
            for (var i = 0; i < values.Count; i++)
                toreturn[i] = values[i].ToString();

            return toreturn;
        }

        
        public static string GetSid(this SearchResultEntry result)
        {
            if (!result.Attributes.Contains("objectsid"))
                return null;

            var s = result.Attributes["objectsid"][0];
            switch (s)
            {
                case byte[] b:
                    return new SecurityIdentifier(b, 0).ToString();
                case string st:
                    return new SecurityIdentifier(Encoding.ASCII.GetBytes(st), 0).ToString();
            }

            return null;
        }
    }
}
