﻿using System;
using DRE.Mathematics;


/*
 * http://cabbynode.net/2012/06/10/perlin-simplex-noise-for-c-and-xna/
 * 
 * A speed-improved simplex noise algorithm for 2D, 3D and 4D in Java.
 *
 * Based on example code by Stefan Gustavson (stegu@itn.liu.se).
 * Optimisations by Peter Eastman (peastman@drizzle.stanford.edu).
 * Better rank ordering method by Stefan Gustavson in 2012.
 *
 * This could be speeded up even further, but it's useful as it is.
 *
 * Version 2012-03-09
 *
 * This code was placed in the public domain by its original author,
 * Stefan Gustavson. You may use it as you see fit, but
 * attribution is appreciated.
 *
 * Update by NightCabbage (2013-11-05) NightCabbage@gmail.com
 * 
 * Working with Stefan (thanks!) I have compiled all of the
 * improvements I could find and put them into this code.
 * 
 * Note that for corner contribution I have made the decision here to
 * use 0.6 instead of 0.5, as I believe it looks a bit better for 2d
 * purposes (0.5 made it a bit more grey, and also had more pulsating for
 * integral inputs). If you're using it for bumpmaps or similar, feel
 * free to change it - and the final scale factor is 76 (as opposed to 32).
 */



/*
So a while back I was checking out different noise methods for procedural and art generation, and I decided to look into Simplex Noise.

	Simplex Noise is basically Ken Perlin’s replacement for classic Perlin Noise. It produces better results, and is a bit faster, too.

		I found a really good paper called “Simplex noise demystified”, by Stefan Gustavson, Linköping University (2005).

		In it was an algorithm written in Java for 2d, 3d and 4d noise. I ported it to C# for 2d and 3d noise. I didn’t bother porting the 4d noise over, because I’d never use it.

			The method signature is as follows:
			public static float GetNoise(double pX, double pY, double pZ)

			So, you’d call it like this (note that I’ve encapsulated it in a static class called Noise):
			float noiseValue = Noise.GetNoise(x, y, z);

It takes doubles for input, as the extra accuracy over floats is needed (floats suck for positional data!), and it returns a single float (between 0 and 1) for the value of the noise at the specified point.

	This method is extra handy because you can use it in the following ways…

	* Simple 2d noise
	Noise.GetValue(x, y, 0);
and you can replace the z value of 0 with any number you want, giving you a different 2d noise pattern each time (just be sure to use the same Z value for the entire “block” of noise!)
or you could keep z as 0 and modify the x and/or y values to get a different noise pattern, eg. Noise.GetNoise(x + 100, y + 100, 0);

* Animated 2d noise
Noise.GetValue(x, y, timer);
	where timer is a value that changes over time, and you can modify the x, y and/or timer values as above to produce different patterns

* Simple 3d noise
Noise.GetValue(x, y, z);
full 3d noise, and if you want a different pattern, all you need to do is modify any/all of the values, eg. Noise.GetNoise(x + 100, y + 100, z + 100);

Hope you enjoy the code! :)
*/


namespace DRE.Noise
{
	public class SimplexNoise : INoise
	{
		#region Source data

		// Class to speed up gradient computations (array access is a lot slower than member access)
		private struct Grad
		{
			public double x, y, z, w;

			public Grad(double x, double y, double z, double w = 0)
			{
				this.x = x;
				this.y = y;
				this.z = z;
				this.w = w;
			}
		}

		private static Grad[] grad3 = new Grad[] {
			new Grad(1,1,0), new Grad(-1,1,0), new Grad(1,-1,0), new Grad(-1,-1,0),
			new Grad(1,0,1), new Grad(-1,0,1), new Grad(1,0,-1), new Grad(-1,0,-1),
			new Grad(0,1,1), new Grad(0,-1,1), new Grad(0,1,-1), new Grad(0,-1,-1)
		};

		private static Grad[] grad4 = new Grad[] {
			new Grad(0,1,1,1),new Grad(0,1,1,-1),new Grad(0,1,-1,1),new Grad(0,1,-1,-1),
			new Grad(0,-1,1,1),new Grad(0,-1,1,-1),new Grad(0,-1,-1,1),new Grad(0,-1,-1,-1),
			new Grad(1,0,1,1),new Grad(1,0,1,-1),new Grad(1,0,-1,1),new Grad(1,0,-1,-1),
			new Grad(-1,0,1,1),new Grad(-1,0,1,-1),new Grad(-1,0,-1,1),new Grad(-1,0,-1,-1),
			new Grad(1,1,0,1),new Grad(1,1,0,-1),new Grad(1,-1,0,1),new Grad(1,-1,0,-1),
			new Grad(-1,1,0,1),new Grad(-1,1,0,-1),new Grad(-1,-1,0,1),new Grad(-1,-1,0,-1),
			new Grad(1,1,1,0),new Grad(1,1,-1,0),new Grad(1,-1,1,0),new Grad(1,-1,-1,0),
			new Grad(-1,1,1,0),new Grad(-1,1,-1,0),new Grad(-1,-1,1,0),new Grad(-1,-1,-1,0)
		};


		// Permutation table source with default values. Used only as a source for calculations of permutation tables used in the algorithm.
		private ushort[] permSource = new ushort[] {
			151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,190,6,148,
			247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,
			74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54,
			65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,
			52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,
			119,248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,
			218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,
			184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
		};

		// Permutation tables used in the algorithm
		// To remove the need for index wrapping, double the permutation table length
		private ushort[] perm = new ushort[512];
		private ushort[] permMod12 = new ushort[512];


		// Skewing and unskewing factors for 2, 3, and 4 dimensions
		private static double F2 = 0.5*(Math.Sqrt(3.0)-1.0);
		private static double G2 = (3.0-Math.Sqrt(3.0))/6.0;
		private static double F3 = 1.0/3.0;
		private static double G3 = 1.0/6.0;
		private static double F4 = (Math.Sqrt(5.0)-1.0)/4.0;
		private static double G4 = (5.0-Math.Sqrt(5.0))/20.0;

		#endregion


		#region Constructors

		/// If seed is zero, default source values will be used (output values will be always the same at given points).
		/// Otherwise source data will be randomised using a default random generator initialised with given seed.
		public SimplexNoise(ulong seed = 0)
		{
			if (seed > 0) {
				IRandom random = new XorShift128PlusRandom(seed);
				CalculatePermutationTable(random);
			} else {
				CalculatePermutationTable(null);
			}
		}


		/// Otherwise source data will be randomised using a given random generator.
		public SimplexNoise(IRandom random)
		{
			CalculatePermutationTable(random);
		}


		/// Must be called to initialise source data. Otherwise you will get a non-randomised pattern.
		private void CalculatePermutationTable(IRandom random)
		{
			// Generate random permutation table source (overwrite the default one)
			if (random != null)
			{
				for (uint i = 0; i < 256; i++)
				{
					permSource[i] = (ushort)random.NextUInt(256);
				}
			}

			// Calculate permutation tables
			for (int i = 0; i < 512; i++)
			{
				perm[i] = permSource[i & 255];
				permMod12[i] = (ushort)(perm[i] % 12);
			}
		}

		#endregion


		#region Math

		/// This method is a *lot* faster than using (int)Math.floor(x)
		private int Floor(double x)
		{
			int xi = (int)x;
			return x < xi ? xi - 1 : xi;
		}


		/// 2D dot product based on gradient
		private double Dot(Grad g, double x, double y)
		{
			return g.x * x + g.y * y;
		}

		/// 3D dot product based on gradient
		private double Dot(Grad g, double x, double y, double z)
		{
			return g.x * x + g.y * y + g.z * z;
		}

		/// 4D dot product based on gradient
		private double Dot(Grad g, double x, double y, double z, double w)
		{
			return g.x * x + g.y * y + g.z * z + g.w * w;
		}

		#endregion


		#region Noise calculations

		/// Returns value in range <-1,1> based on 1D simplex noise
		public double GetValue(double x)
		{
			// TODO: Implement 1D simplex noise
			return GetValue(x, 0);
		}


		/// Returns value in range <-1,1> based on 2D simplex noise
		public double GetValue(double x, double y)
		{
			double n0, n1, n2; // Noise contributions from the three corners
			// Skew the input space to determine which simplex cell we're in
			double s = (x+y)*F2; // Hairy factor for 2D
			int i = Floor(x+s);
			int j = Floor(y+s);
			double t = (i+j)*G2;
			double X0 = i-t; // Unskew the cell origin back to (x,y) space
			double Y0 = j-t;
			double x0 = x-X0; // The x,y distances from the cell origin
			double y0 = y-Y0;
			// For the 2D case, the simplex shape is an equilateral triangle.
			// Determine which simplex we are in.
			int i1, j1; // Offsets for second (middle) corner of simplex in (i,j) coords
			if(x0>y0) {i1=1; j1=0;} // lower triangle, XY order: (0,0)->(1,0)->(1,1)
			else {i1=0; j1=1;}      // upper triangle, YX order: (0,0)->(0,1)->(1,1)
			// A step of (1,0) in (i,j) means a step of (1-c,-c) in (x,y), and
			// a step of (0,1) in (i,j) means a step of (-c,1-c) in (x,y), where
			// c = (3-sqrt(3))/6
			double x1 = x0 - i1 + G2; // Offsets for middle corner in (x,y) unskewed coords
			double y1 = y0 - j1 + G2;
			double x2 = x0 - 1.0 + 2.0 * G2; // Offsets for last corner in (x,y) unskewed coords
			double y2 = y0 - 1.0 + 2.0 * G2;
			// Work out the hashed gradient indices of the three simplex corners
			int ii = i & 255;
			int jj = j & 255;
			int gi0 = permMod12[ii+perm[jj]];
			int gi1 = permMod12[ii+i1+perm[jj+j1]];
			int gi2 = permMod12[ii+1+perm[jj+1]];
			// Calculate the contribution from the three corners
			double t0 = 0.5 - x0*x0-y0*y0;
			if(t0<0) { n0 = 0.0; }
			else {
				t0 *= t0;
				n0 = t0 * t0 * Dot(grad3[gi0], x0, y0);  // (x,y) of grad3 used for 2D gradient
			}
			double t1 = 0.5 - x1*x1-y1*y1;
			if(t1<0) { n1 = 0.0; }
			else {
				t1 *= t1;
				n1 = t1 * t1 * Dot(grad3[gi1], x1, y1);
			}
			double t2 = 0.5 - x2*x2-y2*y2;
			if(t2<0) { n2 = 0.0; }
			else {
				t2 *= t2;
				n2 = t2 * t2 * Dot(grad3[gi2], x2, y2);
			}
			// Add contributions from each corner to get the final noise value.
			// The result is scaled to return values in the interval <-1,1>.
			return 70.0 * (n0 + n1 + n2);
		}


		/// Returns value in range <-1,1> based on 3D simplex noise
		public double GetValue(double x, double y, double z)
		{
			double n0, n1, n2, n3; // Noise contributions from the four corners
			// Skew the input space to determine which simplex cell we're in
			double s = (x+y+z)*F3; // Very nice and simple skew factor for 3D
			int i = Floor(x+s);
			int j = Floor(y+s);
			int k = Floor(z+s);
			double t = (i+j+k)*G3;
			double X0 = i-t; // Unskew the cell origin back to (x,y,z) space
			double Y0 = j-t;
			double Z0 = k-t;
			double x0 = x-X0; // The x,y,z distances from the cell origin
			double y0 = y-Y0;
			double z0 = z-Z0;
			// For the 3D case, the simplex shape is a slightly irregular tetrahedron.
			// Determine which simplex we are in.
			int i1, j1, k1; // Offsets for second corner of simplex in (i,j,k) coords
			int i2, j2, k2; // Offsets for third corner of simplex in (i,j,k) coords
			if(x0>=y0) {
				if(y0>=z0)
				{ i1=1; j1=0; k1=0; i2=1; j2=1; k2=0; } // X Y Z order
				else if(x0>=z0) { i1=1; j1=0; k1=0; i2=1; j2=0; k2=1; } // X Z Y order
				else { i1=0; j1=0; k1=1; i2=1; j2=0; k2=1; } // Z X Y order
			}
			else { // x0<y0
				if(y0<z0) { i1=0; j1=0; k1=1; i2=0; j2=1; k2=1; } // Z Y X order
				else if(x0<z0) { i1=0; j1=1; k1=0; i2=0; j2=1; k2=1; } // Y Z X order
				else { i1=0; j1=1; k1=0; i2=1; j2=1; k2=0; } // Y X Z order
			}
			// A step of (1,0,0) in (i,j,k) means a step of (1-c,-c,-c) in (x,y,z),
			// a step of (0,1,0) in (i,j,k) means a step of (-c,1-c,-c) in (x,y,z), and
			// a step of (0,0,1) in (i,j,k) means a step of (-c,-c,1-c) in (x,y,z), where
			// c = 1/6.
			double x1 = x0 - i1 + G3; // Offsets for second corner in (x,y,z) coords
			double y1 = y0 - j1 + G3;
			double z1 = z0 - k1 + G3;
			double x2 = x0 - i2 + 2.0*G3; // Offsets for third corner in (x,y,z) coords
			double y2 = y0 - j2 + 2.0*G3;
			double z2 = z0 - k2 + 2.0*G3;
			double x3 = x0 - 1.0 + 3.0*G3; // Offsets for last corner in (x,y,z) coords
			double y3 = y0 - 1.0 + 3.0*G3;
			double z3 = z0 - 1.0 + 3.0*G3;
			// Work out the hashed gradient indices of the four simplex corners
			int ii = i & 255;
			int jj = j & 255;
			int kk = k & 255;
			int gi0 = permMod12[ii+perm[jj+perm[kk]]];
			int gi1 = permMod12[ii+i1+perm[jj+j1+perm[kk+k1]]];
			int gi2 = permMod12[ii+i2+perm[jj+j2+perm[kk+k2]]];
			int gi3 = permMod12[ii+1+perm[jj+1+perm[kk+1]]];
			// Calculate the contribution from the four corners
			// Original = 0.5, modified = 0.6
			// According to the PDF document, it should be 0.5, not 0.6, else the noise is not continuous at simplex boundaries. Same for 4D case. Is it true??? If yes, we also need to change the multiplier from 32 to 76, because the range is smaller.
			const double tt = 0.6;
			double t0 = tt - x0 * x0 - y0 * y0 - z0 * z0;
			if(t0<0) n0 = 0.0;
			else {
				t0 *= t0;
				n0 = t0 * t0 * Dot(grad3[gi0], x0, y0, z0);
			}
			double t1 = tt - x1 * x1 - y1 * y1 - z1 * z1;
			if(t1<0) n1 = 0.0;
			else {
				t1 *= t1;
				n1 = t1 * t1 * Dot(grad3[gi1], x1, y1, z1);
			}
			double t2 = tt - x2 * x2 - y2 * y2 - z2 * z2;
			if(t2<0) n2 = 0.0;
			else {
				t2 *= t2;
				n2 = t2 * t2 * Dot(grad3[gi2], x2, y2, z2);
			}
			double t3 = tt - x3 * x3 - y3 * y3 - z3 * z3;
			if(t3<0) n3 = 0.0;
			else {
				t3 *= t3;
				n3 = t3 * t3 * Dot(grad3[gi3], x3, y3, z3);
			}
			// Add contributions from each corner to get the final noise value.
			// Change the multiplier to 76.0 if you want (original is 32.0)
			//return (32.0 * (n0 + n1 + n2 + n3) + 1) * 0.5f; // Range <0,1>
			return 32.0 * (n0 + n1 + n2 + n3); // Range <-1,1>
		}


		/// Returns value in range <-1,1> based on 4D simplex noise. Better simplex rank ordering method 2012-03-09
		public double GetValue(double x, double y, double z, double w)
		{
			double n0, n1, n2, n3, n4; // Noise contributions from the five corners
			// Skew the (x,y,z,w) space to determine which cell of 24 simplices we're in
			double s = (x + y + z + w) * F4; // Factor for 4D skewing
			int i = Floor(x + s);
			int j = Floor(y + s);
			int k = Floor(z + s);
			int l = Floor(w + s);
			double t = (i + j + k + l) * G4; // Factor for 4D unskewing
			double X0 = i - t; // Unskew the cell origin back to (x,y,z,w) space
			double Y0 = j - t;
			double Z0 = k - t;
			double W0 = l - t;
			double x0 = x - X0;  // The x,y,z,w distances from the cell origin
			double y0 = y - Y0;
			double z0 = z - Z0;
			double w0 = w - W0;
			// For the 4D case, the simplex is a 4D shape I won't even try to describe.
			// To find out which of the 24 possible simplices we're in, we need to
			// determine the magnitude ordering of x0, y0, z0 and w0.
			// Six pair-wise comparisons are performed between each possible pair
			// of the four coordinates, and the results are used to rank the numbers.
			int rankx = 0;
			int ranky = 0;
			int rankz = 0;
			int rankw = 0;
			if(x0 > y0) rankx++; else ranky++;
			if(x0 > z0) rankx++; else rankz++;
			if(x0 > w0) rankx++; else rankw++;
			if(y0 > z0) ranky++; else rankz++;
			if(y0 > w0) ranky++; else rankw++;
			if(z0 > w0) rankz++; else rankw++;
			int i1, j1, k1, l1; // The integer offsets for the second simplex corner
			int i2, j2, k2, l2; // The integer offsets for the third simplex corner
			int i3, j3, k3, l3; // The integer offsets for the fourth simplex corner
			// simplex[c] is a 4-vector with the numbers 0, 1, 2 and 3 in some order.
			// Many values of c will never occur, since e.g. x>y>z>w makes x<z, y<w and x<w
			// impossible. Only the 24 indices which have non-zero entries make any sense.
			// We use a thresholding to set the coordinates in turn from the largest magnitude.
			// Rank 3 denotes the largest coordinate.
			i1 = rankx >= 3 ? 1 : 0;
			j1 = ranky >= 3 ? 1 : 0;
			k1 = rankz >= 3 ? 1 : 0;
			l1 = rankw >= 3 ? 1 : 0;
			// Rank 2 denotes the second largest coordinate.
			i2 = rankx >= 2 ? 1 : 0;
			j2 = ranky >= 2 ? 1 : 0;
			k2 = rankz >= 2 ? 1 : 0;
			l2 = rankw >= 2 ? 1 : 0;
			// Rank 1 denotes the second smallest coordinate.
			i3 = rankx >= 1 ? 1 : 0;
			j3 = ranky >= 1 ? 1 : 0;
			k3 = rankz >= 1 ? 1 : 0;
			l3 = rankw >= 1 ? 1 : 0;
			// The fifth corner has all coordinate offsets = 1, so no need to compute that.
			double x1 = x0 - i1 + G4; // Offsets for second corner in (x,y,z,w) coords
			double y1 = y0 - j1 + G4;
			double z1 = z0 - k1 + G4;
			double w1 = w0 - l1 + G4;
			double x2 = x0 - i2 + 2.0*G4; // Offsets for third corner in (x,y,z,w) coords
			double y2 = y0 - j2 + 2.0*G4;
			double z2 = z0 - k2 + 2.0*G4;
			double w2 = w0 - l2 + 2.0*G4;
			double x3 = x0 - i3 + 3.0*G4; // Offsets for fourth corner in (x,y,z,w) coords
			double y3 = y0 - j3 + 3.0*G4;
			double z3 = z0 - k3 + 3.0*G4;
			double w3 = w0 - l3 + 3.0*G4;
			double x4 = x0 - 1.0 + 4.0*G4; // Offsets for last corner in (x,y,z,w) coords
			double y4 = y0 - 1.0 + 4.0*G4;
			double z4 = z0 - 1.0 + 4.0*G4;
			double w4 = w0 - 1.0 + 4.0*G4;
			// Work out the hashed gradient indices of the five simplex corners
			int ii = i & 255;
			int jj = j & 255;
			int kk = k & 255;
			int ll = l & 255;
			int gi0 = perm[ii+perm[jj+perm[kk+perm[ll]]]] % 32;
			int gi1 = perm[ii+i1+perm[jj+j1+perm[kk+k1+perm[ll+l1]]]] % 32;
			int gi2 = perm[ii+i2+perm[jj+j2+perm[kk+k2+perm[ll+l2]]]] % 32;
			int gi3 = perm[ii+i3+perm[jj+j3+perm[kk+k3+perm[ll+l3]]]] % 32;
			int gi4 = perm[ii+1+perm[jj+1+perm[kk+1+perm[ll+1]]]] % 32;
			// Calculate the contribution from the five corners
			// According to the PDF document, it should be 0.5, not 0.6, else the noise is not continuous at simplex boundaries. Is it true??? Are the values spread in whole range <-1,1> ???
			double t0 = 0.6 - x0*x0 - y0*y0 - z0*z0 - w0*w0;
			if(t0<0) n0 = 0.0;
			else {
				t0 *= t0;
				n0 = t0 * t0 * Dot(grad4[gi0], x0, y0, z0, w0);
			}
			double t1 = 0.6 - x1*x1 - y1*y1 - z1*z1 - w1*w1;
			if(t1<0) n1 = 0.0;
			else {
				t1 *= t1;
				n1 = t1 * t1 * Dot(grad4[gi1], x1, y1, z1, w1);
			}
			double t2 = 0.6 - x2*x2 - y2*y2 - z2*z2 - w2*w2;
			if(t2<0) n2 = 0.0;
			else {
				t2 *= t2;
				n2 = t2 * t2 * Dot(grad4[gi2], x2, y2, z2, w2);
			}
			double t3 = 0.6 - x3*x3 - y3*y3 - z3*z3 - w3*w3;
			if(t3<0) n3 = 0.0;
			else {
				t3 *= t3;
				n3 = t3 * t3 * Dot(grad4[gi3], x3, y3, z3, w3);
			}
			double t4 = 0.6 - x4*x4 - y4*y4 - z4*z4 - w4*w4;
			if(t4<0) n4 = 0.0;
			else {
				t4 *= t4;
				n4 = t4 * t4 * Dot(grad4[gi4], x4, y4, z4, w4);
			}
			// Sum up and scale the result to cover the range <-1,1>
			return 27.0 * (n0 + n1 + n2 + n3 + n4);
		}

		#endregion
	}
}