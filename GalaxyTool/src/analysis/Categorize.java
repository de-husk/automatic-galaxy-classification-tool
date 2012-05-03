package analysis;

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Scanner;

public class Categorize {
	public static void main(String args[]) throws IOException {
		//TODO: remove below and change to params argument (its unprofessional to direct link)
		if (args.length != 1) {
			System.out.println("ERROR: Missing or Extra Arguments");
		} else {
			String classificationPath = args[0];			
			File classifications = new File(classificationPath);
			Scanner in = new Scanner(classifications);
			ArrayList<Integer> classificationList = new ArrayList<Integer>();
			// -6 class --> 0 (gets mapped to 0) (everything is shifted over 6
			// places for algorithmic ease)
			int classCount[] = new int[18];
			String skip = in.nextLine();
	
			while (in.hasNextLine()) {
				String cur = in.nextLine();
				String params[] = cur.split(" ");
				classCount[Integer.parseInt(params[2]) + 6]++;
				classificationList.add(Integer.parseInt(params[2]));
			}
	
			FileWriter fstream = new FileWriter("Results/"
					+ "classificationAnalysis.txt");
			BufferedWriter out = new BufferedWriter(fstream);
			int ellipseTot = 0;
			int spiralTot = 0;
			int irregTot = 0;
			double tot = 0.0;
	
			System.out.println("Class totals: ");
			out.write("Class totals: " + "\n");
	
			for (int i = 0; i < classCount.length; i++) {
				System.out.println(i - 6 + "total :" + classCount[i]);
				out.write(i - 6 + "total :" + classCount[i] + "\n");
				if (i <= 2) {
					ellipseTot += classCount[i];
				} else if (i <= 14 && i > 2) {
					spiralTot += classCount[i];
				} else {
					irregTot += classCount[i];
				}
				tot += classCount[i];
			}
	
			out.write("Total: " + tot + "\n");
			out.write("Ellipse Total: " + ellipseTot + "\n");
			out.write("Ellipse Percent: " + ellipseTot / tot * 100 + "\n");
			out.write("Sprial Total: " + spiralTot + "\n");
			out.write("Sprial Percent: " + spiralTot / tot * 100 + "\n");
			out.write("Irregular Total " + irregTot + "\n");
			out.write("Irregular Percent " + irregTot / tot * 100 + "\n");
	
			out.flush();
			out.close();
		}
	}
}
