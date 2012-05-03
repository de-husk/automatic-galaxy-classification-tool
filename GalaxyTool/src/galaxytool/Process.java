package galaxytool;

import java.awt.Color;
import java.awt.Graphics;
import java.awt.Graphics2D;
import java.awt.RenderingHints;
import java.awt.image.BufferedImage;
import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import javax.imageio.ImageIO;

import org.apache.commons.io.IOUtils;

import weka.classifiers.meta.FilteredClassifier;
import weka.classifiers.trees.M5P;
import weka.core.Instances;
import weka.core.converters.ConverterUtils.DataSource;
import weka.filters.unsupervised.attribute.Remove;
import Jama.Matrix;
import Jama.SingularValueDecomposition;

public class Process {

	public boolean readyToClassify = false;
	public double rmse = 0.0;
	private Instances data;
	public FilteredClassifier fc;

	private static String[][] galaxyData;//TODO: I changed below look at later
	private static int imageScaleSize = 49; // look at (3)
	private static final int classCount = 18;
	private static final int sv = 25; // look at (3)
	// public static Evaluation eval;
	static SingularValueDecomposition svdR;
	static SingularValueDecomposition svdG;
	static SingularValueDecomposition svdB;
	static Matrix dataRed;
	static Matrix dataGreen;
	static Matrix dataBlue;
	static Matrix frV;
	static Matrix fgV;
	static Matrix fbV;

	public int classify(int index) throws Exception {
		int classPrediction = (int) Math.round(fc.classifyInstance(data
				.instance(index)));
		if (classPrediction < -6) {
			classPrediction = -6;
		} else if (classPrediction > 11) {
			classPrediction = 11;
		}

		return classPrediction;
	}

	// file path to a folder of galaxy images to classify
	public void classify(String filePath) {
		try {
			//TODO: make sure to check if it is an image
			//	before processing
			
			File root = new File(filePath);	
			System.out.println("Processing multiple images");
			File[] images = root.listFiles();
			System.out.println("Num of galaxies to classify: "+ images.length);			
			
			// sample the images for svd
			int sampleFactor = 4;			
			
			// Create folder
			File f = new File("Results/");
			f.mkdir();
			FileWriter fstream = new FileWriter("Results/" + "temp.arff");
			BufferedWriter out = new BufferedWriter(fstream);

			out.write("@relation 'galaxy'\n");
			out.write("@attribute class real\n");
			out.write("@attribute colorF real\n");
			out.write("@attribute bulgeF real\n");
			out.write("@attribute constF real\n");
			for (int i = 0; i < sv * 3; i++) {
				out.write("@attribute " + i + " real\n");
			}
			out.write("@data\n");
			//TODO: this will break for large enough folders
					
			// fill the 'full' datasets
			dataRed = new Matrix(images.length, imageScaleSize
					* imageScaleSize);
			dataGreen = new Matrix(images.length, imageScaleSize
					* imageScaleSize);
			dataBlue = new Matrix(images.length, imageScaleSize
					* imageScaleSize);			
			
			//create the entire size of the dataset
			for (int imageIndex = 0; imageIndex < images.length; imageIndex++) {				
				BufferedImage tempImage = getImage(images[imageIndex].getPath(), imageScaleSize);

				for (int i = 0; i < imageScaleSize; i++) {
					for (int j = 0; j < imageScaleSize; j++) {
						long pixelColor = tempImage.getRGB(i, j);
						int rgb[] = new int[3];
						rgb[0] += ((pixelColor & 0x00ff0000) >> 16);
						rgb[1] += ((pixelColor & 0x0000ff00) >> 8);
						rgb[2] += (pixelColor & 0x000000ff);

						dataRed.set(imageIndex, i * imageScaleSize + j, rgb[0]);
						dataGreen.set(imageIndex, i * imageScaleSize + j,
								rgb[1]);
						dataBlue.set(imageIndex, i * imageScaleSize + j, rgb[2]);
					}
				}
				System.out.println("SDSS Galaxy " + imageIndex + " put in main dataset");
			}

			
			//then convert the whole dataset to the same coordinate
			// Do the coordinate conversion
			Matrix rV = dataRed.times(frV);
			Matrix gV = dataGreen.times(fgV);
			Matrix bV = dataBlue.times(fbV);

			System.out.println("Dim Final rU: " + rV.getColumnDimension()
					+ ", " + rV.getRowDimension());
			System.out.println("images.length: " + images.length);
			
			// write to the output file here:
			for (int imageIndex = 0; imageIndex < images.length; imageIndex++) {

				BufferedImage tempImage = getImage(images[imageIndex].getPath(), imageScaleSize);

				float colorFactor = (getColor(tempImage)[0] / getColor(tempImage)[2]); // /
																						// getColor(tempImage)[1];
				// float chiralityFactor = 1.0f + Math.abs((-10.0f *
				// getChirality(tempImage) - classChirality[nnNumber]) /
				// classChirality[nnNumber]);
				float centralBulgeFactor = getCentralBulge(tempImage);
				float consistencyFactor = getConsistency(tempImage);

				
				//TODO: should i remove the class?
				out.write(-999 + ", ");
				//TODO: right now im just putting -999 because it is unclassified			
				
				out.write(colorFactor + ", ");
				out.write(centralBulgeFactor + ", ");
				out.write(consistencyFactor + ", ");

				// output data (r,g,b)
				for (int i = 0; i < rV.getColumnDimension(); i++) {
					out.write(rV.get(imageIndex, i) + ", ");
					out.write(gV.get(imageIndex, i) + ", ");
					if (i == rV.getColumnDimension() - 1) {
						out.write(bV.get(imageIndex, i) + "\n");
					} else {
						out.write(bV.get(imageIndex, i) + ", ");
					}
				}
			}

			out.flush();
			out.close();

			System.out.println("Finished creating testing arff file...");
			
			// create the list of image[] indices and their associated Equitorial Coords.
			ArrayList <Coordinate> coords = new ArrayList <Coordinate>();
			
			//parse the file names and intialize coords arraylist
			for(int i = 0; i < images.length; i++){				
				String re1=".*?";	
			    String re2="([+-]?\\d*\\.\\d+)(?![-+0-9\\.])";	// Double 1
			    String re3=".*?";	
			    String re4="([+-]?\\d*\\.\\d+)(?![-+0-9\\.])";	// Double 2			    
			    Pattern p = Pattern.compile(re1+re2+re3+re4,Pattern.CASE_INSENSITIVE | Pattern.DOTALL);
			    Matcher m = p.matcher(images[i].getName());
			    if(m.find()){
				    System.out.println(images[i].getName());	
				    System.out.println(m.group(1));
				    System.out.println(m.group(2));
				    coords.add(i, new Coordinate(Double.parseDouble(m.group(1)), Double.parseDouble(m.group(2))));
			    }
			}
			
			System.out.println("Start classifiying the images in "+ filePath);
			
			fstream = new FileWriter("Results/" + "Final Classifications.txt");
			out = new BufferedWriter(fstream);
			out.write("RA,	DEC,	Classification" + "\n");
			
			DataSource source = new DataSource("Results/temp.arff");
			Instances tests = source.getDataSet();
			tests.setClassIndex(0);

			for(int i = 0; i < tests.numInstances(); i ++){
				int classPrediction = (int) Math.round(fc.classifyInstance(tests
						.instance(i)));
				if (classPrediction < -6) {
					classPrediction = -6;
				} else if (classPrediction > 11) {
					classPrediction = 11;
				}				
				out.write(coords.get(i).getRA() + ", " + coords.get(i).getDec() + ", " + classPrediction + "\n");
				if(i%100 == 0){
					File fo = new File("Results/SampleOutput");
					fo.mkdir();
					File cf = new File(fo.getAbsolutePath() + "/Class " + (classPrediction) + "/");
					cf.mkdir();
					
					//every 100th one save in its sample output folder
					File img = new File(cf.getAbsolutePath() + "/"+ images[i].getName());
					FileInputStream inStream = new FileInputStream(images[i]);
					OutputStream outStream = new FileOutputStream(img, true);
					try {
						IOUtils.copy(inStream, outStream);
					} finally {
						IOUtils.closeQuietly(inStream);
						IOUtils.closeQuietly(outStream);
					}

					
				}
			}			
			
			out.flush();
			out.close();			
			
			System.out.println("Finished classifiying in " + filePath);
			
		} catch (Exception ex) {
			System.out.println(ex.toString());
		}
	}

	public int classify(BufferedImage currentImage) {
		try {
			ImageIO.write(currentImage, "png", new File("Results/temp.png"));

			// Create folder
			File f = new File("Results/");
			f.mkdir();
			FileWriter fstream = new FileWriter("Results/" + "temp.arff");
			BufferedWriter out = new BufferedWriter(fstream);

			out.write("@relation 'galaxy'\n");
			out.write("@attribute class real\n");
			out.write("@attribute colorF real\n");
			out.write("@attribute bulgeF real\n");
			out.write("@attribute constF real\n");
			for (int i = 0; i < sv * 3; i++) {
				out.write("@attribute " + i + " real\n");
			}
			out.write("@data\n");

			BufferedImage tempImage = getImage("Results/temp.png",
					imageScaleSize);

			for (int i = 0; i < imageScaleSize; i++) {
				for (int j = 0; j < imageScaleSize; j++) {
					long pixelColor = tempImage.getRGB(i, j);
					int rgb[] = new int[3];
					rgb[0] += ((pixelColor & 0x00ff0000) >> 16);
					rgb[1] += ((pixelColor & 0x0000ff00) >> 8);
					rgb[2] += (pixelColor & 0x000000ff);

					dataRed.set(galaxyData.length - 1, i * imageScaleSize + j,
							rgb[0]);
					dataGreen.set(galaxyData.length - 1,
							i * imageScaleSize + j, rgb[1]);
					dataBlue.set(galaxyData.length - 1, i * imageScaleSize + j,
							rgb[2]);
				}
			}

			// perform the SVD here:
			int threadCount = Thread.activeCount();

			SvdThread t[] = new SvdThread[3];

			for (int i = 0; i < t.length; i++) {
				t[i] = new SvdThread(i);
				t[i].start();
			}

			while (Thread.activeCount() > threadCount) {
				Thread.yield();
			}

			Matrix rU = svdR.getU();
			Matrix gU = svdG.getU();
			Matrix bU = svdB.getU();
			rU = getSVs(rU, sv);
			gU = getSVs(gU, sv);
			bU = getSVs(bU, sv);

			float colorFactor = (getColor(tempImage)[0] / getColor(tempImage)[2]); // /
																					// getColor(tempImage)[1];
			// float chiralityFactor = 1.0f + Math.abs((-10.0f *
			// getChirality(tempImage) - classChirality[nnNumber]) /
			// classChirality[nnNumber]);
			float centralBulgeFactor = getCentralBulge(tempImage);
			float consistencyFactor = getConsistency(tempImage);

			out.write(galaxyData[galaxyData.length - 1][1] + ", ");
			out.write(colorFactor + ", ");
			out.write(centralBulgeFactor + ", ");
			out.write(consistencyFactor + ", ");

			// output data (r,g,b)
			for (int i = 0; i < rU.getColumnDimension(); i++) {
				out.write(rU.get(galaxyData.length - 1, i) + ", ");
				out.write(gU.get(galaxyData.length - 1, i) + ", ");
				if (i == rU.getColumnDimension() - 1) {
					out.write(bU.get(galaxyData.length - 1, i) + "\n");
				} else {
					out.write(bU.get(galaxyData.length - 1, i) + ", ");
				}
			}

			out.flush();
			out.close();

			DataSource source = new DataSource("Results/temp.arff");
			Instances test = source.getDataSet();
			test.setClassIndex(0);

			int classPrediction = (int) Math.round(fc.classifyInstance(test
					.instance(0)));
			if (classPrediction < -6) {
				classPrediction = -6;
			} else if (classPrediction > 11) {
				classPrediction = 11;
			}

			return classPrediction;

		} catch (Exception ex) {
			System.out.println(ex.toString());
		}

		return -99;
	}

	class SvdThread extends Thread {

		int color;

		SvdThread(int color) {
			this.color = color;
		}

		@Override
		public void run() {
			switch (color) {
			case 0:
				svdR = new SingularValueDecomposition(dataRed);
				break;
			case 1:
				svdG = new SingularValueDecomposition(dataGreen);
				break;
			case 2:
				svdB = new SingularValueDecomposition(dataBlue);
				break;
			}
		}
	}

	public void run() throws Exception {
		fillGalaxyData();
		getFiles();
		train();
	}

	private void train() throws Exception {
		System.out.println("Begin training on Efigi galaxies...");

		M5P tree = new M5P();
		String[] options = new String[1];
		DataSource source = new DataSource("Results/" + "resultsGalaxy.arff");
		data = source.getDataSet();
		data.setClassIndex(0);
		tree.buildClassifier(data);

		// eval = new Evaluation(data);
		// eval.crossValidateModel(tree, data, 10, new Random(1));

		File out = new File("Results/" + "classification.txt");
		FileWriter fstream = new FileWriter(out);

		rmse = 0.0;
		int classifiedCount = 0;

		Remove rm = new Remove();
		rm.setInputFormat(data);
		fc = new FilteredClassifier();
		fc.setFilter(rm);
		fc.setClassifier(tree);

		for (int i = 0; i < data.numInstances(); i++) {

			int classPrediction = (int) Math.round(fc.classifyInstance(data
					.instance(i)));
			if (classPrediction < -6) {
				classPrediction = -6;
			} else if (classPrediction > 11) {
				classPrediction = 11;
			}

			int actualClass = (int) Math.round(data.instance(i).classValue());

			int error = Math.abs(classPrediction - actualClass);
			rmse += error * error;
			classifiedCount++;

			fstream.write("\n" + classPrediction + ", " + error);
		}

		rmse = Math.sqrt(rmse / classifiedCount);
		fstream.write("\nRMSE: " + rmse);
		fstream.flush();
		fstream.close();

		readyToClassify = true;

		System.out.println("Finished training on Efigi galaxies");
	}

	/**
	 * Computes the Moore–Penrose pseudoinverse using the SVD method.
	 * 
	 * Modified version of the original implementation by Kim van der Linde.
	 */
	public static Matrix pinv(Matrix x) {
		if (x.rank() < 1)
			return null;

		if (x.getColumnDimension() > x.getRowDimension())
			return pinv(x.transpose()).transpose();

		SingularValueDecomposition svdX = new SingularValueDecomposition(x);
		double[] singularValues = svdX.getSingularValues();
		double tol = Math.max(x.getColumnDimension(), x.getRowDimension())
				* singularValues[0] * 2E-16;

		double[] singularValueReciprocals = new double[singularValues.length];
		for (int i = 0; i < singularValues.length; i++)
			singularValueReciprocals[i] = Math.abs(singularValues[i]) < tol ? 0
					: (1.0 / singularValues[i]);

		double[][] u = svdX.getU().getArray();
		double[][] v = svdX.getV().getArray();

		int min = Math.min(x.getColumnDimension(), u[0].length);

		double[][] inverse = new double[x.getColumnDimension()][x
				.getRowDimension()];

		for (int i = 0; i < x.getColumnDimension(); i++)
			for (int j = 0; j < u.length; j++)
				for (int k = 0; k < min; k++)
					inverse[i][j] += v[i][k] * singularValueReciprocals[k]
							* u[j][k];
		return new Matrix(inverse);
	}

	private void getFiles() {
		try {
			// sampleFactor is the amount to divide by the total size of the
			// data set
			// when determining the subsample that will be used in svd

			int sampleFactor = 4;

			System.out.println("Creating arff file...");

			// Create folder
			File f = new File("Results/");
			f.mkdir();
			FileWriter fstream = new FileWriter("Results/"
					+ "resultsGalaxy.arff");
			BufferedWriter out = new BufferedWriter(fstream);

			out.write("@relation 'galaxy'\n");
			out.write("@attribute class real\n");
			out.write("@attribute colorF real\n");
			out.write("@attribute bulgeF real\n");
			out.write("@attribute constF real\n");
			for (int i = 0; i < sv * 3; i++) {
				out.write("@attribute " + i + " real\n");
			}
			out.write("@data\n");

			// Initialize a three matrices that will hold all of the images
			// (r,g,b of each image where each row is an image)
			dataRed = new Matrix(galaxyData.length / sampleFactor,
					imageScaleSize * imageScaleSize);
			dataGreen = new Matrix(galaxyData.length / sampleFactor,
					imageScaleSize * imageScaleSize);
			dataBlue = new Matrix(galaxyData.length / sampleFactor,
					imageScaleSize * imageScaleSize);

			// subsample from galaxydata
			int curIndex = 0;
			for (int jk = 0; jk < galaxyData.length / sampleFactor; jk++) {
				BufferedImage tempImage = getImage("galaxies/"
						+ galaxyData[curIndex][0] + ".jpg", imageScaleSize);

				for (int i = 0; i < imageScaleSize; i++) {
					for (int j = 0; j < imageScaleSize; j++) {
						long pixelColor = tempImage.getRGB(i, j);
						int rgb[] = new int[3];
						rgb[0] += ((pixelColor & 0x00ff0000) >> 16);
						rgb[1] += ((pixelColor & 0x0000ff00) >> 8);
						rgb[2] += (pixelColor & 0x000000ff);

						dataRed.set(jk, i * imageScaleSize + j, rgb[0]);
						dataGreen.set(jk, i * imageScaleSize + j, rgb[1]);
						dataBlue.set(jk, i * imageScaleSize + j, rgb[2]);
					}
				}
				System.out.println("Galaxy " + curIndex + " finished");

				// calculate next curIndex
				curIndex += sampleFactor;
			}

			// Perform svd on subsample:
			int threadCount = Thread.activeCount();

			SvdThread t[] = new SvdThread[3];

			for (int i = 0; i < t.length; i++) {
				t[i] = new SvdThread(i);
				t[i].start();
			}

			while (Thread.activeCount() > threadCount) {
				Thread.yield();
			}

			// Create the basis for each component
			Matrix rV = svdR.getV();
			System.out.println("dim rU: " + rV.getRowDimension() + ", "
					+ rV.getColumnDimension());
			Matrix gV = svdG.getV();
			Matrix bV = svdB.getV();

			rV = getSVs(rV, sv);
			System.out.println("Svs: " + sv);
			System.out.println("Dim SsV: " + rV.getRowDimension() + ", "
					+ rV.getColumnDimension());
			gV = getSVs(gV, sv);
			bV = getSVs(bV, sv);

			// Perform the pseudoinverses
			frV = pinv(rV.transpose());
			fgV = pinv(gV.transpose());
			fbV = pinv(bV.transpose());

			// fill the 'full' datasets
			dataRed = new Matrix(galaxyData.length, imageScaleSize
					* imageScaleSize);
			dataGreen = new Matrix(galaxyData.length, imageScaleSize
					* imageScaleSize);
			dataBlue = new Matrix(galaxyData.length, imageScaleSize
					* imageScaleSize);

			for (int imageIndex = 0; imageIndex < galaxyData.length; imageIndex++) {

				BufferedImage tempImage = getImage("galaxies/"
						+ galaxyData[imageIndex][0] + ".jpg", imageScaleSize);

				for (int i = 0; i < imageScaleSize; i++) {
					for (int j = 0; j < imageScaleSize; j++) {
						long pixelColor = tempImage.getRGB(i, j);
						int rgb[] = new int[3];
						rgb[0] += ((pixelColor & 0x00ff0000) >> 16);
						rgb[1] += ((pixelColor & 0x0000ff00) >> 8);
						rgb[2] += (pixelColor & 0x000000ff);

						dataRed.set(imageIndex, i * imageScaleSize + j, rgb[0]);
						dataGreen.set(imageIndex, i * imageScaleSize + j,
								rgb[1]);
						dataBlue.set(imageIndex, i * imageScaleSize + j, rgb[2]);
					}
				}
				System.out.println("Galaxy " + imageIndex + " finished");
			}

			// Do the coordinate conversion
			rV = dataRed.times(frV);
			gV = dataGreen.times(fgV);
			bV = dataBlue.times(fbV);

			System.out.println("Dim Final rU: " + rV.getColumnDimension()
					+ ", " + rV.getRowDimension());
			System.out.println("galaxydata.length: " + galaxyData.length);

			// write to the output file here:
			for (int imageIndex = 0; imageIndex < galaxyData.length; imageIndex++) {

				BufferedImage tempImage = getImage("galaxies/"
						+ galaxyData[imageIndex][0] + ".jpg", imageScaleSize);

				float colorFactor = (getColor(tempImage)[0] / getColor(tempImage)[2]); // /
																						// getColor(tempImage)[1];
				// float chiralityFactor = 1.0f + Math.abs((-10.0f *
				// getChirality(tempImage) - classChirality[nnNumber]) /
				// classChirality[nnNumber]);
				float centralBulgeFactor = getCentralBulge(tempImage);
				float consistencyFactor = getConsistency(tempImage);

				out.write(galaxyData[imageIndex][1] + ", ");
				out.write(colorFactor + ", ");
				out.write(centralBulgeFactor + ", ");
				out.write(consistencyFactor + ", ");

				// output data (r,g,b)
				for (int i = 0; i < rV.getColumnDimension(); i++) {
					out.write(rV.get(imageIndex, i) + ", ");
					out.write(gV.get(imageIndex, i) + ", ");
					if (i == rV.getColumnDimension() - 1) {
						out.write(bV.get(imageIndex, i) + "\n");
					} else {
						out.write(bV.get(imageIndex, i) + ", ");
					}
				}
			}

			out.flush();
			out.close();

			System.out.println("Finished creating arff file...");

		} catch (Exception ex) {
			System.out.println(ex.toString());
		}
	}

	void printMatrix(Matrix matrix, BufferedWriter out) throws IOException {
		for (int j = 0; j < matrix.getColumnDimension(); j++) {
			for (int i = 0; i < matrix.getRowDimension(); i++) {
				out.write(matrix.get(i, j) + ", ");
			}
			out.write("\n");
		}
		out.write("\n");
	}

	// Take SV columns from the U matrix
	private Matrix getSVs(Matrix matrix, int sv) {
		Matrix toPass = new Matrix(matrix.getRowDimension(), sv);
		for (int j = 0; j < sv; j++) {
			for (int i = 0; i < matrix.getRowDimension(); i++) {
				// toPass.set(j*(sv)+i, j, matrix.get(i, j));
				toPass.set(i, j, matrix.get(i, j));
			}
		}
		return toPass;
	}

	private float[] getColor(BufferedImage img) {
		float[] rgb = new float[3];
		int imgWidth = img.getWidth();

		for (int x = 0; x < imgWidth; x++) {
			for (int y = 0; y < imgWidth; y++) {
				long pixelColor = img.getRGB(x, y);
				rgb[0] += ((pixelColor & 0x00ff0000) >> 16);
				rgb[1] += ((pixelColor & 0x0000ff00) >> 8);
				rgb[2] += (pixelColor & 0x000000ff);
			}
		}
		rgb[0] = rgb[0] / (imgWidth * imgWidth * 256.0f);
		rgb[1] = rgb[1] / (imgWidth * imgWidth * 256.0f);
		rgb[2] = rgb[2] / (imgWidth * imgWidth * 256.0f);

		return rgb;
	}

	private float getConsistency(BufferedImage img) {
		int imgWidth = img.getWidth();
		if (img.getHeight() < imgWidth) {
			imgWidth = img.getHeight();
		}

		float[] consistencyArray = new float[imgWidth / 2];

		for (int i = 0; i < consistencyArray.length; i++) {
			consistencyArray[i] = 0;
		}

		for (int radius = 0; radius < consistencyArray.length; radius++) {
			for (float angle = 0.0f; angle < 2.0f * Math.PI; angle += Math.PI / 180.0f) {
				int x = (int) (Math.cos(angle) * radius + imgWidth / 2.0f);
				int y = (int) (Math.sin(angle) * radius + imgWidth / 2.0f);

				long pixelColor = img.getRGB(x, y);
				int red = (int) ((pixelColor & 0x00ff0000) >> 16);
				int green = (int) ((pixelColor & 0x0000ff00) >> 8);
				int blue = (int) (pixelColor & 0x000000ff);
				int brightness = (red + green + blue) / 3;

				consistencyArray[radius] += brightness * brightness
						/ consistencyArray.length;
			}
		}

		for (int i = 0; i < consistencyArray.length; i++) {
			consistencyArray[i] = (float) Math.sqrt(consistencyArray[i]);
		}

		for (int i = consistencyArray.length - 1; i > 0; i--) {
			consistencyArray[i] = (consistencyArray[i - 1] - consistencyArray[i]); // /
			// (float)(Math.PI
			// *
			// i
			// +
			// 1.0f);
			// System.out.println(consistencyArray[i]);
		}
		// consistencyArray[0] = 0;

		float average = 0.0f;
		for (int i = 0; i < consistencyArray.length; i++) {
			average += consistencyArray[i]; // / (consistencyArray.length * 2 *
			// Math.PI * i + 1);
		}
		average = average / (float) consistencyArray.length;

		float stdDev = 0.0f;
		for (int i = 0; i < consistencyArray.length; i++) {
			stdDev += (average - consistencyArray[i])
					* (average - consistencyArray[i]) / (256.0f * 256.0f);
		}
		stdDev = (float) Math.sqrt(stdDev / (float) consistencyArray.length);

		if (Math.abs(average) < 1) { // Avoid division by 0
			average = 1;
		}

		// System.out.println(stdDev + ", " + average);

		return stdDev;
	}

	private float getImageRotationAngle(BufferedImage img) {
		float widestAngle = 0.0f;
		int imgWidth = img.getWidth();
		if (img.getHeight() < imgWidth) {
			imgWidth = img.getHeight();
		}
		int avgBrightness = 0;

		for (int x = 0; x < imgWidth; x++) {
			for (int y = 0; y < imgWidth; y++) {
				long pixelColor = img.getRGB(x, y);
				int red = (int) ((pixelColor & 0x00ff0000) >> 16);
				int green = (int) ((pixelColor & 0x0000ff00) >> 8);
				int blue = (int) (pixelColor & 0x000000ff);
				avgBrightness += (red + green + blue) / 3;
			}
		}
		avgBrightness = avgBrightness / (imgWidth * imgWidth);

		int brightnessOfRadius = 0;
		int bestRadialBrightness = 0;

		for (float angle = 0.0f; angle < 2.0f * Math.PI; angle += Math.PI / 180.0f) {
			for (float radius = -imgWidth / 2.0f; radius < imgWidth / 2.0f; radius++) {
				int x = (int) (Math.cos(angle) * radius + imgWidth / 2.0f);
				int y = (int) (Math.sin(angle) * radius + imgWidth / 2.0f);

				long pixelColor = img.getRGB(x, y);
				int red = (int) ((pixelColor & 0x00ff0000) >> 16);
				int green = (int) ((pixelColor & 0x0000ff00) >> 8);
				int blue = (int) (pixelColor & 0x000000ff);
				int brightness = (red + green + blue) / 3;

				brightnessOfRadius += brightness;
			}
			if (brightnessOfRadius > bestRadialBrightness) {
				bestRadialBrightness = brightnessOfRadius;
				widestAngle = angle;
			}
			brightnessOfRadius = 0;
		}

		return -widestAngle;
	}

	private float getCentralBulge(BufferedImage img) {
		int w = img.getWidth();
		int h = img.getHeight();
		float lastAvg = 0;
		float peakDecline = 0.0f;
		float peakDeclineCount = 0.0f;

		for (float radius = 1; radius < w / 2; radius++) {
			float radialAvg = 0.0f;
			for (float angle = 0.0f; angle < 2.0f * Math.PI; angle += Math.PI / 45.0f) {
				int x = (int) (Math.cos(angle) * radius) + w / 2;
				int y = (int) (Math.sin(angle) * radius) + h / 2;

				long pixelColor = img.getRGB(x, y);
				int red = (int) ((pixelColor & 0x00ff0000) >> 16);
				int green = (int) ((pixelColor & 0x0000ff00) >> 8);
				int blue = (int) (pixelColor & 0x000000ff);
				radialAvg += (red + green + blue) / 3;
			}
			radialAvg = radialAvg / 90.0f;

			// if (lastAvg - radialAvg > radialAvg / 6) {
			peakDecline += radius * (lastAvg - radialAvg) / (44); // 36
			peakDeclineCount++;
			// }

			lastAvg = radialAvg;
		}

		if (peakDeclineCount == 0) {
			return -1;
		}

		return peakDecline / peakDeclineCount;
	}

	private float getChirality(BufferedImage image) {
		float chirality = 0f;
		int width = image.getWidth();
		int height = image.getHeight();
		float angleSum = 0f;
		float currentX = width / 2, currentY = height / 2;
		float radDistance = 0.1f; // 0.2f?
		float lastAngle = 0.0f;

		for (float i = 0.0f; i < width * radDistance; i++) {
			float brightnessSum = 0f, brightestSum = 0f, brightestAngle = 0f;

			for (float angle = -(float) Math.PI / 2.0f + lastAngle; angle < Math.PI
					/ 2.0f + lastAngle; angle += Math.PI / 180.0f) {
				brightnessSum = 0;

				for (float radius = 1; radius < width * radDistance; radius++) {
					int x = (int) (Math.cos(angle) * radius + currentX);
					int y = (int) (Math.sin(angle) * radius + currentY);

					if (x > width - 1) {
						x = width - 1;
						i = width * radDistance;
						angle = (float) Math.PI / 2.0f + lastAngle;
						radius = width * radDistance;
					} else if (x < 0) {
						x = 0;
						i = width * radDistance;
						angle = (float) Math.PI / 2.0f + lastAngle;
						radius = width * radDistance;
					}

					if (y > height - 1) {
						y = height - 1;
						i = width * radDistance;
						angle = (float) Math.PI / 2.0f + lastAngle;
						radius = width * radDistance;
					} else if (y < 0) {
						y = 0;
						i = width * radDistance;
						angle = (float) Math.PI / 2.0f + lastAngle;
						radius = width * radDistance;
					}

					long pixelColor = image.getRGB(x, y);
					int red = (int) ((pixelColor & 0x00ff0000) >> 16);
					int green = (int) ((pixelColor & 0x0000ff00) >> 8);
					int blue = (int) (pixelColor & 0x000000ff);
					int brightness = (red + green + blue) / 3;

					brightnessSum += brightness;
				}

				if (brightnessSum > brightestSum) {
					brightestAngle = angle;
					brightestSum = brightnessSum;
				}
			}

			angleSum += brightestAngle;
			lastAngle = brightestAngle;
			currentX += (int) (Math.cos(brightestAngle) * width * (0.15));
			currentY += (int) (Math.sin(brightestAngle) * width * (0.15));
		}
		chirality = (angleSum * radDistance) / width;

		return chirality;
	}

	private BufferedImage stretchImage(BufferedImage img) {
		int w = img.getWidth();
		int h = img.getHeight();
		int dimmestYValue = 255;
		int dimmestXValue = 255;

		for (int i = 0; i < w / 2; i++) {
			long pixelColor = img.getRGB(w / 2 + i, h / 2);
			int red = (int) ((pixelColor & 0x00ff0000) >> 16);
			int green = (int) ((pixelColor & 0x0000ff00) >> 8);
			int blue = (int) (pixelColor & 0x000000ff);
			int color = (red + green + blue) / 3;
			if (color < dimmestXValue) {
				dimmestXValue = color;
			}
		}

		for (int i = 0; i < h / 2; i++) {
			long pixelColor = img.getRGB(w / 2, h / 2 + i);
			int red = (int) ((pixelColor & 0x00ff0000) >> 16);
			int green = (int) ((pixelColor & 0x0000ff00) >> 8);
			int blue = (int) (pixelColor & 0x000000ff);
			int color = (red + green + blue) / 3;
			if (color < dimmestYValue) {
				dimmestYValue = color;
			}
		}

		int currentBrightness = 255;
		int actualGalaxyWidth = 0;
		while (currentBrightness > 2 * dimmestXValue
				&& actualGalaxyWidth < w / 2) {
			long pixelColor = img.getRGB(w / 2 + actualGalaxyWidth, h / 2);
			int red = (int) ((pixelColor & 0x00ff0000) >> 16);
			int green = (int) ((pixelColor & 0x0000ff00) >> 8);
			int blue = (int) (pixelColor & 0x000000ff);
			currentBrightness = (red + green + blue) / 3;

			actualGalaxyWidth++;
		}

		currentBrightness = 255;
		int actualGalaxyHeight = 0;
		while (currentBrightness > 2 * dimmestYValue
				&& actualGalaxyHeight < h / 2) {
			long pixelColor = img.getRGB(w / 2, h / 2 + actualGalaxyHeight);
			int red = (int) ((pixelColor & 0x00ff0000) >> 16);
			int green = (int) ((pixelColor & 0x0000ff00) >> 8);
			int blue = (int) (pixelColor & 0x000000ff);
			currentBrightness = (red + green + blue) / 3;

			actualGalaxyHeight++;
		}

		float imgRatio = (float) actualGalaxyWidth / (float) actualGalaxyHeight;
		if (imgRatio > 2.0f) {
			BufferedImage result = new BufferedImage(img.getWidth(),
					img.getHeight(), BufferedImage.TYPE_INT_RGB);
			Graphics g = result.getGraphics();
			g.drawImage(img, 0, 0, w, h, 0,
					(int) (h * (1.0f / imgRatio + 1.0f) / 2.0f), w,
					(int) (h * (1.0f - (1.0f / imgRatio + 1.0f) / 2.0f)), null);
			img.flush();
			g.dispose();

			return result;
		} else {
			return img;
		}
	}

	private BufferedImage getImage(String fileName, int scaledResolution) {
		BufferedImage source = null;
		BufferedImage result = null;

		try {
			// Read the source image file and scale with temp image, store back
			// to source
			source = ImageIO.read(new File(fileName));
			result = new BufferedImage(source.getWidth(), source.getHeight(),
					BufferedImage.TYPE_INT_RGB);

			Graphics2D tempContext = (Graphics2D) (result.getGraphics());
			tempContext.setColor(Color.black);
			tempContext.clearRect(0, 0, result.getWidth(), result.getHeight());
			tempContext.setRenderingHint(RenderingHints.KEY_INTERPOLATION,
					RenderingHints.VALUE_INTERPOLATION_BICUBIC);
			tempContext.setRenderingHint(RenderingHints.KEY_RENDERING,
					RenderingHints.VALUE_RENDER_QUALITY);
			tempContext.rotate(getImageRotationAngle(source),
					source.getWidth() / 2, source.getHeight() / 2);
			tempContext.drawImage(source, 0, 0, null);
			result.flush();
			tempContext.dispose();

			if (getConsistency(result) > 1.0f) {
				result = stretchImage(result);
			}

			float chirality = getChirality(source);

			if (chirality < 0) {
				// flip the image
				int w = result.getWidth();
				int h = result.getHeight();
				source = new BufferedImage(scaledResolution, scaledResolution,
						BufferedImage.TYPE_INT_RGB);
				Graphics resultContext = source.getGraphics();
				resultContext.drawImage(result, scaledResolution, 0, 0,
						scaledResolution, 0, 0, w, h, null);
			} else {
				source = new BufferedImage(scaledResolution, scaledResolution,
						BufferedImage.TYPE_INT_RGB);
				Graphics resultContext = source.getGraphics();
				resultContext.drawImage(result, 0, 0, scaledResolution,
						scaledResolution, null);
			}
			source.flush();

			// ImageIO.write(source, "png", new File("temp/" + fileName));
		} catch (Exception ex) {
			System.out.println("Failed to open: " + fileName + ", "
					+ ex.toString());
		}
		return source;
	}

	private void fillGalaxyData() {
		try {
			System.out.println("Loading galaxy description data");

			int headerLength = 82;
			BufferedReader in = new BufferedReader(new FileReader(
					"galaxies/EFIGI_attributes.txt"));

			// Find data length
			int dataLength = 0;
			while (in.ready()) {
				in.readLine();
				dataLength++;
			}
			dataLength -= headerLength;
			galaxyData = new String[dataLength][2];
			in.close();

			// Restart at beginning now that we know size of data file, move
			// past header info
			in = new BufferedReader(new FileReader(
					"galaxies/EFIGI_attributes.txt"));
			for (int i = 0; i < headerLength; i++) {
				in.readLine();
			}
			for (int i = 0; i < galaxyData.length; i++) {
				String[] readLine = in.readLine().split(" ");
				int temp = 1;
				while (readLine[temp].equals("")) {
					temp++;
				}
				galaxyData[i][0] = readLine[0]; // Galaxy data file name
				galaxyData[i][1] = readLine[temp]; // Galaxy class
			}

			/*
			 * for(int i = 0; i < galaxyData.length; i++) {
			 * System.out.println(galaxyData[i][0] + ": " + galaxyData[i][1]); }
			 */

		} catch (Exception ex) {
			System.out.println("Error reading galaxy data file");
		}
	}
}
