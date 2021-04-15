using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DHIS2Xfer.Factory
{
    public class XferFactory
    {
        public enum FileType
        {
            Server = 0,
            CategoryCombo = 1,
            CategoryOptionCombo = 2,
            Meta = 3,
            Job = 4,
            OrgUnits = 5,
            OrgUnitGroup = 6,
            OUMapping = 7,
            OrgUnitLevels = 8,
            Xfer = 9,
            XferMeta = 10
        }

        public static string GetTypeExtension(FileType type)
        {
            switch (type)
            {
                case FileType.Server:
                    return ".srv";
                case FileType.CategoryCombo:
                    return ".cc";
                case FileType.CategoryOptionCombo:
                    return ".coc";
                case FileType.Meta:
                    return ".meta";
                case FileType.Job:
                    return ".jb";
                case FileType.OrgUnits:
                    return ".ou";
                case FileType.OrgUnitGroup:
                    return ".oug";
                case FileType.OUMapping:
                    return ".oum";
                case FileType.OrgUnitLevels:
                    return ".oul";
                case FileType.Xfer:
                    return ".xfer";
                case FileType.XferMeta:
                    return ".xfermeta";
                default:
                    return "";
            }
        }

        public static string GetTypeFolder(FileType type)
        {
            switch (type)
            {
                case FileType.Server:
                    return "Servers";
                case FileType.CategoryCombo:
                    return "CategoryCombo";
                case FileType.CategoryOptionCombo:
                    return "CategoryOptionCombo";
                case FileType.Meta:
                    return "Meta";
                case FileType.Job:
                    return "Jobs";
                case FileType.OrgUnits:
                    return "OrganisationUnits";
                case FileType.OrgUnitGroup:
                    return "OrganisationUnitGroups";
                case FileType.OUMapping:
                    return "OrganisationUnitMapping";
                case FileType.OrgUnitLevels:
                    return "OrgUnitLevels";
                case FileType.Xfer:
                case FileType.XferMeta:
                    return "Xfer";
                default:
                    return "";
            }
        }

        public static JObject OUAutoMap(string source, string dest,string directory)
        {
            //Get server data
            JObject serverSource = DataFactory.GetServer(source, directory);
            JObject destSource = DataFactory.GetServer(dest, directory);

            //Get org unit data
            JObject orgSource = DataFactory.GetOrgUnits(source, directory);
            JObject orgDest = DataFactory.GetOrgUnits(dest, directory);

            JObject OUMapped = new JObject();
            JArray mappedList = new JArray();
            JArray unmappedList = new JArray();

            //TODO: Add server sync date to OUMapped
            JObject meta = new JObject();
            meta["source"] = serverSource.GetValue("ID");
            meta["destination"] = destSource.GetValue("ID");
            meta["sourceSync"] = serverSource.GetValue("lastSync");
            meta["destSync"] = destSource.GetValue("lastSync");


            JArray ouSource = (JArray)orgSource.GetValue("organisationUnits");
            JArray ouDest = (JArray)orgDest.GetValue("organisationUnits");

            bool isMapped = false;

            //Map source to destination
            foreach (JObject s in ouSource)
            {
                foreach (JObject d in ouDest)
                {
                    if(s.GetValue("displayName").ToString() == d.GetValue("displayName").ToString())
                    {
                        JObject mapped = new JObject();
                        mapped["sourceID"] = s.GetValue("id");
                        mapped["destID"] = d.GetValue("id");
                        mapped["srcName"] = s.GetValue("displayName");
                        mapped["destName"] = d.GetValue("displayName");

                        mappedList.Add(mapped);

                        isMapped = true;
                        break;
                    }
                }

                //If no match, then add to unmapped list
                if (!isMapped)
                {
                    s["type"] = "Source";
                    unmappedList.Add(s);
                }

                //reset the flag
                isMapped = false;
            }

            //Go through the destination list and see whats not mapped
            foreach (JObject d in ouDest)
            {
                foreach (JObject m in mappedList)
                {
                    if (d.GetValue("displayName").ToString() == m.GetValue("destName").ToString())
                    {
                       
                        isMapped = true;
                        break;
                    }
                }

                //If no match, then add to unmapped list
                if (!isMapped)
                {
                    d["type"] = "Destination";
                    unmappedList.Add(d);
                }

                //reset the flag
                isMapped = false;
            }


            OUMapped["meta"] = meta;
            OUMapped["mapped"] = mappedList;
            OUMapped["unmapped"] = unmappedList;

            return OUMapped;
        }

        public static JObject verifyOuMapping(JObject ouMap, string source, string dest, string directory)
        {
            JObject meta = (JObject)ouMap.GetValue("meta");

            //Get server data
            JObject serverSource = DataFactory.GetServer(source, directory);
            JObject destSource = DataFactory.GetServer(dest, directory);

            //Check if server sync date matches what is in the ouMap
            //If the server sync dates match, then the file is up to date. 
            if (meta.GetValue("sourceSync").ToString() == serverSource.GetValue("lastSync").ToString() && meta.GetValue("destSync").ToString() == destSource.GetValue("lastSync").ToString())
                return ouMap;

            //Else verify the ouMap file against the source and destination Org Unit files
            
            //Get org unit data
            JObject orgSource = DataFactory.GetOrgUnits(source, directory);
            JObject orgDest = DataFactory.GetOrgUnits(dest, directory);

            JArray updateMapping = new JArray();
            JArray unmapped = new JArray();

            //Check source org units 
            foreach (JObject srcOrgUnit in orgSource.GetValue("organisationUnits"))
            {
                bool match = false;

                foreach (JObject mapped in ouMap.GetValue("mapped"))
                {
                    if(mapped.GetValue("sourceID").ToString() == srcOrgUnit.GetValue("id").ToString())
                    {
                        match = true;
                        break;
                    }
                }

                //If no match, add to the unmapped list
                if (!match)
                {
                    srcOrgUnit["type"] = "Source";
                    unmapped.Add(srcOrgUnit);
                }
            }

            //Check destination org units
            foreach (JObject destOrgUnit in orgDest.GetValue("organisationUnits"))
            {
                bool match = false;

                foreach (JObject mapped in ouMap.GetValue("mapped"))
                {
                    if (mapped.GetValue("destID").ToString() == destOrgUnit.GetValue("id").ToString())
                    {
                        match = true;
                        break;
                    }
                }

                //If no match, add to the unmapped list
                if (!match)
                {
                    destOrgUnit["type"] = "Destination";
                    unmapped.Add(destOrgUnit);
                }
            }


            return ouMap;
        }

        public static JObject jobAutoMap(string source, string dest,string sourceType,string destinationType, string directory)
        {
            //Get server data
            JObject serverSource = DataFactory.GetServer(source, directory);
            JObject destSource = DataFactory.GetServer(dest, directory);

            //Get meta data
            JObject metaSource = FileFactory.GetFile(source + GetTypeAbbrev(sourceType), XferFactory.FileType.Meta, directory);
            JObject metaDest = FileFactory.GetFile(dest + GetTypeAbbrev(destinationType), XferFactory.FileType.Meta, directory);

            JObject jobs = new JObject();

            JArray mapped = new JArray();
            JArray unMapped = new JArray();

            bool isMapped = false;

            //Automap source meta to destination meta
            foreach (JObject src in metaSource.GetValue(sourceType ))
            {
               

                //If no match, then add to unmapped list
                if (!isMapped)
                {
                    src["type"] = "Source";
                    unMapped.Add(src);
                }

                isMapped = false;
            }

            //Go through the destination list and see whats not mapped
            foreach (JObject d in metaDest.GetValue(destinationType))
            {

                //If no match, then add to unmapped list
                if (!isMapped)
                {
                    d["type"] = "Destination";
                    unMapped.Add(d);
                }

                //reset the flag
                isMapped = false;
            }

            jobs["mapped"] = mapped;
            jobs["unMapped"] = unMapped;

            return jobs;
        }

        public static string GetTypeAbbrev(string type)
        {
            string abbrev = "";

            switch (type)
            {
                case "dataElements":
                    abbrev = "-DE";
                    break;
                case "indicators":
                    abbrev = "-IND";
                    break;
                case "programIndicators":
                    abbrev = "-PRG";
                    break;
            }

            return abbrev;
        }
    }
}
