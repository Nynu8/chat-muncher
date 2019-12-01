using System;
using System.Collections.Generic;
using System.Text;

namespace StarSonataApi
{
    public class TeamCharacter
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public short Rank { get; set; }

        public int LastOnline { get; set; }
    }
}
