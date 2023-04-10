﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace localChess.Networking
{
    internal class Packet
    {
        public int Id { get; set; } = 0;
        public List<long> Nonce = new();

        public Packet()
        {
            var rng = new Random();
            for (var i = 0; i < 4; i++)
            {
                Nonce.Add(rng.NextInt64());
            }
        }
    }
}
