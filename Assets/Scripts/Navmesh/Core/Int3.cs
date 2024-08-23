using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Navmesh.Core
{
    public struct Int3
    {
        public int x;
        public int y;
        public int z;

		private static Int3 _zero = new Int3(0, 0, 0);
		public static Int3 zero { get { return _zero; } }

		/** Precision for the integer coordinates.
		 * One world unit is divided into [value] pieces. A value of 1000 would mean millimeter precision, a value of 1 would mean meter precision (assuming 1 world unit = 1 meter).
		 * This value affects the maximum coordinates for nodes as well as how large the cost values are for moving between two nodes.
		 * A higher value means that you also have to set all penalty values to a higher value to compensate since the normal cost of moving will be higher.
		 */
		public const int Precision = 1000;

		/** #Precision as a float */
		public const float FloatPrecision = 1000F;

		/** 1 divided by #Precision */
		public const float PrecisionFactor = 0.001F;

		public Int3(UnityEngine.Vector3 position)
		{
			x = (int)System.Math.Round(position.x * FloatPrecision);
			y = (int)System.Math.Round(position.y * FloatPrecision);
			z = (int)System.Math.Round(position.z * FloatPrecision);
			//x = Mathf.RoundToInt (position.x);
			//y = Mathf.RoundToInt (position.y);
			//z = Mathf.RoundToInt (position.z);
		}


		public Int3(int _x, int _y, int _z)
		{
			x = _x;
			y = _y;
			z = _z;
		}

		public static bool operator ==(Int3 lhs, Int3 rhs)
		{
			return lhs.x == rhs.x &&
					lhs.y == rhs.y &&
					lhs.z == rhs.z;
		}

		public static bool operator !=(Int3 lhs, Int3 rhs)
		{
			return lhs.x != rhs.x ||
					lhs.y != rhs.y ||
					lhs.z != rhs.z;
		}

		public static explicit operator Int3(UnityEngine.Vector3 ob)
		{
			return new Int3(
				(int)System.Math.Round(ob.x * FloatPrecision),
				(int)System.Math.Round(ob.y * FloatPrecision),
				(int)System.Math.Round(ob.z * FloatPrecision)
				);
			//return new Int3 (Mathf.RoundToInt (ob.x*FloatPrecision),Mathf.RoundToInt (ob.y*FloatPrecision),Mathf.RoundToInt (ob.z*FloatPrecision));
		}

		public static explicit operator UnityEngine.Vector3(Int3 ob)
		{
			return new UnityEngine.Vector3(ob.x * PrecisionFactor, ob.y * PrecisionFactor, ob.z * PrecisionFactor);
		}

		public static Int3 operator -(Int3 lhs, Int3 rhs)
		{
			lhs.x -= rhs.x;
			lhs.y -= rhs.y;
			lhs.z -= rhs.z;
			return lhs;
		}

		public static Int3 operator -(Int3 lhs)
		{
			lhs.x = -lhs.x;
			lhs.y = -lhs.y;
			lhs.z = -lhs.z;
			return lhs;
		}

		public static Int3 operator +(Int3 lhs, Int3 rhs)
		{
			lhs.x += rhs.x;
			lhs.y += rhs.y;
			lhs.z += rhs.z;
			return lhs;
		}

		public static Int3 operator *(Int3 lhs, int rhs)
		{
			lhs.x *= rhs;
			lhs.y *= rhs;
			lhs.z *= rhs;

			return lhs;
		}

		public static Int3 operator *(Int3 lhs, float rhs)
		{
			lhs.x = (int)System.Math.Round(lhs.x * rhs);
			lhs.y = (int)System.Math.Round(lhs.y * rhs);
			lhs.z = (int)System.Math.Round(lhs.z * rhs);

			return lhs;
		}

		public static Int3 operator *(Int3 lhs, double rhs)
		{
			lhs.x = (int)System.Math.Round(lhs.x * rhs);
			lhs.y = (int)System.Math.Round(lhs.y * rhs);
			lhs.z = (int)System.Math.Round(lhs.z * rhs);

			return lhs;
		}

		public static Int3 operator *(Int3 lhs, UnityEngine.Vector3 rhs)
		{
			lhs.x = (int)System.Math.Round(lhs.x * rhs.x);
			lhs.y = (int)System.Math.Round(lhs.y * rhs.y);
			lhs.z = (int)System.Math.Round(lhs.z * rhs.z);

			return lhs;
		}

		public static Int3 operator /(Int3 lhs, float rhs)
		{
			lhs.x = (int)System.Math.Round(lhs.x / rhs);
			lhs.y = (int)System.Math.Round(lhs.y / rhs);
			lhs.z = (int)System.Math.Round(lhs.z / rhs);
			return lhs;
		}

		public Int3 DivBy2()
		{
			x >>= 1;
			y >>= 1;
			z >>= 1;
			return this;
		}

		public int this[int i]
		{
			get
			{
				return i == 0 ? x : (i == 1 ? y : z);
			}
			set
			{
				if (i == 0) x = value;
				else if (i == 1) y = value;
				else z = value;
			}
		}

		/** Returns the magnitude of the vector. The magnitude is the 'length' of the vector from 0,0,0 to this point. Can be used for distance calculations:
		  * \code Debug.Log ("Distance between 3,4,5 and 6,7,8 is: "+(new Int3(3,4,5) - new Int3(6,7,8)).magnitude); \endcode
		  */
		public float magnitude
		{
			get
			{
				//It turns out that using doubles is just as fast as using ints with Mathf.Sqrt. And this can also handle larger numbers (possibly with small errors when using huge numbers)!

				double _x = x;
				double _y = y;
				double _z = z;

				return (float)System.Math.Sqrt(_x * _x + _y * _y + _z * _z);

				//return Mathf.Sqrt (x*x+y*y+z*z);
			}
		}

		/** Magnitude used for the cost between two nodes. The default cost between two nodes can be calculated like this:
		  * \code int cost = (node1.position-node2.position).costMagnitude; \endcode
		  *
		  * This is simply the magnitude, rounded to the nearest integer
		  */
		public int costMagnitude
		{
			get
			{
				return (int)System.Math.Round(magnitude);
			}
		}

		public static implicit operator string(Int3 ob)
		{
			return ob.ToString();
		}

		/** Returns a nicely formatted string representing the vector */
		public override string ToString()
		{
			return "( " + x + ", " + y + ", " + z + ")";
		}

		public override bool Equals(System.Object o)
		{

			if (o == null) return false;

			var rhs = (Int3)o;

			return x == rhs.x &&
					y == rhs.y &&
					z == rhs.z;
		}

		public override int GetHashCode()
		{
			return x * 73856093 ^ y * 19349663 ^ z * 83492791;
		}
	}
}
