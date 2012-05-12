using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Datastructures
{
    [Serializable()]
    public class Coordinate
    {
        private double RA;
        private double dec;

        public Coordinate(double RA, double dec)
        {
            this.RA = RA;
            this.dec = dec;
        }

        public double getRA()
        {
            return RA;
        }

        public double getDec()
        {
            return dec;
        }
    }
}