using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ClassificationService
{
	[Serializable()]
	class Categorize
	{
		public static void main(String [] args) {
		if (args.Count() != 1) {
			Console.Write("ERROR: Missing or Extra Arguments");
		} else {
			String classificationPath = args[0];
			StreamReader input = new StreamReader(classificationPath);
			List<int> classificationList = new List<int>();

			// -6 class --> 0 (gets mapped to 0) (everything is shifted over 6
			// places for algorithmic ease)
			int [] classCount = new int[18];
			var skip = input.ReadLine();
	
			while (!input.EndOfStream) {
				String cur = input.ReadLine();
				String [] parms = cur.Split(' ');
				classCount[Convert.ToInt32(parms[2]) + 6]++;
				classificationList.Add(Convert.ToInt32(parms[2]));
			}
	
			StreamWriter output = new StreamWriter("../../../Resuls/" + "classificationAnalysis.txt");
			int ellipseTot = 0;
			int spiralTot = 0;
			int irregTot = 0;
			double tot = 0.0;
	
			Console.Write("Class totals: ");
			output.WriteLine("Class totals: ");
	
			for (int i = 0; i < classCount.Count(); i++) {
				Console.Write(i - 6 + "total :" + classCount[i]);
				output.WriteLine(i - 6 + "total :" + classCount[i]);
				if (i <= 5) {
					ellipseTot += classCount[i];
				} else if (i <= 14) {
					spiralTot += classCount[i];
				} else {
					irregTot += classCount[i];
				}
				tot += classCount[i];
			}
	
			output.Write("Total: " + tot + "\n");
			output.Write("Ellipse Total: " + ellipseTot + "\n");
			output.Write("Ellipse Percent: " + ellipseTot / tot * 100 + "\n");
			output.Write("Sprial Total: " + spiralTot + "\n");
			output.Write("Sprial Percent: " + spiralTot / tot * 100 + "\n");
			output.Write("Irregular Total " + irregTot + "\n");
			output.Write("Irregular Percent " + irregTot / tot * 100 + "\n");
	
			output.Flush();
			output.Close();
			output.Dispose();
		}
	}
	}
}
