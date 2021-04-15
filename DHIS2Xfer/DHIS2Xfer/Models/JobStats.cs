using System.Collections.Generic;

namespace DHIS2Xfer.Models
{
    public class JobStats
    {
        public List<Stats> Stats = new List<Stats>();
        public enum StatType
        {
            SourceDestination,
            Level,
            Period
        }

        private bool HasStat(string name, StatType type)
        {
            foreach (Stats stat in Stats)
            {
                if (stat.Name == name && stat.Type == type)
                    return true;
            }

            return false;
        }

        public void AddStat(string name, int value, StatType type)
        {
            if(HasStat(name,type))
            {
                AddToStat(name, value,type);
            }
            else
            {
                Stats s = new Stats();
                s.Name = name;
                s.Value = value;
                s.Type = type;
                Stats.Add(s);
            }
            

        }

        private void AddToStat(string name, int value, StatType type)
        {
            foreach (Stats stat in Stats)
            {
                if (stat.Name == name && stat.Type == type)
                {
                    stat.Value += value;
                    break;
                }
            }
        }

    }

    public class Stats
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public JobStats.StatType Type { get; set; }
    }
}
