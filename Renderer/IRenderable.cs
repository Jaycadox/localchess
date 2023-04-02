using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace localChess.Renderer
{
    internal interface IRenderable
    {
        public void Render(int x, int y);
    }
}
