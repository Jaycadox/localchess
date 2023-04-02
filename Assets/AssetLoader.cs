using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace localChess.Assets
{
    internal class AssetLoader
    {
        public static string GetPath(string name)
        {
            return "C:\\localChess\\Assets\\" + name;
        }
    }
}
