namespace WNBAScorigami
{
    class WnbaTeam
    {
        public string TeamName { get; }
        public string TeamShortName { get; }
        public bool IsActive { get; }
        public string AltShortNames { get; }

        public WnbaTeam(string name, string shortname, bool active, string altShort = "")
        {
            TeamName = name;
            TeamShortName = shortname;
            IsActive = active;
            AltShortNames = altShort;
        }
    }
}
