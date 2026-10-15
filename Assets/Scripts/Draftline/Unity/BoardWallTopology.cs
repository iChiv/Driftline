using System;
using Draftline.Core;

namespace Draftline.Unity
{
    public static class BoardWallTopology
    {
        // World axes: x=row, z=column. Every exposed face ends at the same exact cell boundary.
        public static bool Occupied(BoardLayout board,int row,int column) => row>=0&&row<19&&column>=0&&column<19
            && !(row==9&&column==9)&&board.CellAt(new GridPosition(column,row)).Kind==CellKind.FixedWall;
        public static int Mask(Func<int,int,bool> occupied,int row,int column) =>
            (occupied(row-1,column)?1:0)|(occupied(row,column+1)?2:0)|(occupied(row+1,column)?4:0)|(occupied(row,column-1)?8:0);
        public static string Shape(int mask)
        {
            mask&=15;int count=0;for(int i=0;i<4;i++)if((mask&(1<<i))!=0)count++;
            return count==0?"Isolated":count==1?"End":count==2?(mask==5||mask==10?"Straight":"Corner"):count==3?"T":"Cross";
        }
    }
}
