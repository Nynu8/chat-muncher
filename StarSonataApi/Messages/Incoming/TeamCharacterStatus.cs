using System;
using System.Collections.Generic;

namespace StarSonataApi.Messages.Incoming
{
    public class TeamCharacterStatus : IIncomingMessage
    {
        public TeamCharacterStatus(byte[] data)
        {
            var byteOffset = 0;
            var id = ByteUtility.GetInt(data, ref byteOffset);
            var name = ByteUtility.GetString(data, ref byteOffset);
            var rank = ByteUtility.GetShort(data, ref byteOffset);
            var lastOnline = ByteUtility.GetInt(data, ref byteOffset);

            this.Character = new TeamCharacter { Id = id, Name = name, Rank = rank, LastOnline = lastOnline };
        }

        public TeamCharacter Character { get; set; }
    }
}
