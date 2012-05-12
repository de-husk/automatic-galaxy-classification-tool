using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using weka.associations;
using weka.attributeSelection;
using weka.classifiers;
using weka.classifiers.meta;
using DotNetMatrix;

namespace ClassificationService {
	[Serializable()]
	public class Logic { //: ICloneable {

		#region " Parent Form Communication "

		public delegate void DisplayMessageInForm(string message);

		public event DisplayMessageInForm DisplayMessage;

		public delegate void DisplayImageInForm(int index);

		public event DisplayImageInForm DisplayImage;

		#endregion


		#region " Global Classifier Properites "

		public bool readyToClassify = false;
		public double rmse = 0.0;
		private weka.core.Instances data;
		public FilteredClassifier fc;

		#endregion


		#region " Private Properties "

		private static String[][] galaxyData;
		private static int imageScaleSize = 50; 
		private const int classCount = 18;
		private const int sv = 25; 
        private const int sampleFactor = 4; 

        private const string OutputDir = "../../../";

		#endregion


		#region " Static Properties "

		// public static Evaluation eval;
		static SingularValueDecomposition svdR;
		static SingularValueDecomposition svdG;
		static SingularValueDecomposition svdB;
		static GeneralMatrix dataRed;
		static GeneralMatrix dataGreen;
		static GeneralMatrix dataBlue;
		static GeneralMatrix frV;
		static GeneralMatrix fgV;
		static GeneralMatrix fbV;

		#endregion


		#region " Presenter Logic "

		public void run() {
			DisplayImage(0);
			fillGalaxyData();
			GetFiles_Decompose_WriteToArff();
			train(false);
		}

        private void WriteFVMatrix(string fileName, double[][] FVMat) {
            MemoryStream ms = new MemoryStream(500000000);
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf =
                new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter(null,
                    new System.Runtime.Serialization.StreamingContext(System.Runtime.Serialization.StreamingContextStates.Clone));
            bf.Serialize(ms, FVMat);
            ms.Seek(0, SeekOrigin.Begin);
            FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            ms.WriteTo(fs);
            fs.Flush();
            fs.Close();
            fs.Dispose();
            ms.Flush();
            ms.Close();
            ms.Dispose();
        }

        private double[][] ReadFVMatrix(int matIndex) {
            //open file from the disk (file path is the path to the file to be opened)
            string file = "frv.mtx";
            if (matIndex == 1) {
                file = "fgv.mtx";
            }
            else if(matIndex == 2) {
                file = "fbv.mtx";
            }

            FileStream fileStream = File.OpenRead(file);
            //create new MemoryStream object
            MemoryStream memStream = new MemoryStream();
            memStream.SetLength(fileStream.Length);
            //read file to MemoryStream
            fileStream.Read(memStream.GetBuffer(), 0, (Convert.ToInt32(fileStream.Length)));

            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter(
                null, new System.Runtime.Serialization.StreamingContext(System.Runtime.Serialization.StreamingContextStates.Clone));
            //GeneralMatrix result = (GeneralMatrix)bf.Deserialize(memStream);
            double [][] result = (double[][])bf.Deserialize(memStream);
            fileStream.Flush();
            fileStream.Close();
            fileStream.Dispose();
            memStream.Flush();
            memStream.Close();
            memStream.Dispose();

            return result;
        }

        public void WriteToFile() {
            WriteFVMatrix("frv.mtx", frV.Array);
            WriteFVMatrix("fgv.mtx", fgV.Array);
            WriteFVMatrix("fbv.mtx", fbV.Array);            
        }

        //public object Clone() {
        //    MemoryStream ms = new MemoryStream(500000000);
        //    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = 
        //        new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter(null, 
        //            new System.Runtime.Serialization.StreamingContext(System.Runtime.Serialization.StreamingContextStates.Clone));
        //    bf.Serialize(ms, this);
        //    ms.Seek(0, SeekOrigin.Begin);
        //    object obj = bf.Deserialize(ms);
        //    ms.Close();
        //    return obj;
        //}

		public void train(bool LoadingFromFile) {
            if (!LoadingFromFile) {
                DisplayMessage("Begin training on Efigi galaxies...");
                Console.Write("Begin training on Efigi galaxies...");
            }
            else {
                DisplayImage(0);
                DisplayMessage("Load from file...");
                Console.Write("Load from file...");

                frV = new GeneralMatrix(ReadFVMatrix(0));
                fgV = new GeneralMatrix(ReadFVMatrix(1));
                fbV = new GeneralMatrix(ReadFVMatrix(2));
            }

			weka.classifiers.trees.M5P tree = new weka.classifiers.trees.M5P();
            
			String[] options = new String[1];
			weka.core.converters.ConverterUtils.DataSource source = new weka.core.converters.ConverterUtils.DataSource(OutputDir + "Results/" + "resultsGalaxy.arff");
			data = source.getDataSet();
            if (data == null) {
                DisplayMessage("Cannot load from file.");
                throw new Exception("Arff File not valid");
            }
			data.setClassIndex(0);
			tree.buildClassifier(data);

			StreamWriter output = new StreamWriter(OutputDir + "Results/" + "classification.txt");

			rmse = 0.0;
			int classifiedCount = 0;

			weka.filters.unsupervised.attribute.Remove rm = new weka.filters.unsupervised.attribute.Remove();
			rm.setInputFormat(data);
			fc = new FilteredClassifier();
			fc.setFilter(rm);
			fc.setClassifier(tree);

			for (int i = 0; i < data.numInstances(); i++) {
				int classPrediction = (int)Math.Round(fc.classifyInstance(data.instance(i)));
				if (classPrediction < -6) {
					classPrediction = -6;
				}
				else if (classPrediction > 11) {
					classPrediction = 11;
				}

				int actualClass = (int)Math.Round(data.instance(i).classValue());

				int error = Math.Abs(classPrediction - actualClass);
				rmse += error * error;
				classifiedCount++;

				output.WriteLine("\n" + classPrediction + ", " + error);
                if (i % 10 == 0 && !LoadingFromFile)
					DisplayImage(i);
			}

			rmse = Math.Sqrt(rmse / classifiedCount);
			output.WriteLine("\nRMSE: " + rmse);
			
			DisplayMessage("RMSE: " + rmse);

			output.Flush();
			output.Close();
			output.Dispose();

			readyToClassify = true;

			Console.WriteLine("Finished training on Efigi galaxies; RMSE: " + rmse.ToString());
		}

		private void GetFiles_Decompose_WriteToArff() {
			try {
				// sampleFactor is the amount to divide by the total size of the
				// data set
				// when determining the subsample that will be used in svd

				Console.WriteLine("Creating arff file...");
				DisplayMessage("Creating arff file...");

				// Create folder
				System.IO.Directory.CreateDirectory(OutputDir + "Results/");
				StreamWriter output = new StreamWriter(OutputDir + "Results/" + "resultsGalaxy.arff");

				output.Write("@relation 'galaxy'\n");
				output.Write("@attribute class real\n");
				output.Write("@attribute colorF real\n");
				output.Write("@attribute bulgeF real\n");
				output.Write("@attribute constF real\n");
				for (int i = 0; i < sv * 3; i++) {
					output.Write("@attribute " + i + " real\n");
				}
				output.Write("@data\n");

				Console.WriteLine("Begin galaxy sampling");
				DisplayMessage("Begin galaxy sampling");

				// Initialize a three matrices that will hold all of the images
				// (r,g,b of each image where each row is an image)
				dataRed = new GeneralMatrix(galaxyData.Count() / sampleFactor,
						imageScaleSize * imageScaleSize);
				dataGreen = new GeneralMatrix(galaxyData.Count() / sampleFactor,
						imageScaleSize * imageScaleSize);
				dataBlue = new GeneralMatrix(galaxyData.Count() / sampleFactor,
						imageScaleSize * imageScaleSize);

				// subsample from galaxydata
				System.Threading.Tasks.Parallel.For(0, galaxyData.Count() / sampleFactor, (int index) => {
					Bitmap tempImage = getImage(OutputDir + "galaxies/"
							+ galaxyData[sampleFactor * index][0] + ".jpg", imageScaleSize);
                    
					for (int i = 0; i < imageScaleSize; i++) {
						for (int j = 0; j < imageScaleSize; j++) {
							int pixelColor = tempImage.GetPixel(i, j).ToArgb();
							int[] rgb = new int[3];
							rgb[0] += ((pixelColor & 0x00ff0000) >> 16);
							rgb[1] += ((pixelColor & 0x0000ff00) >> 8);
							rgb[2] += (pixelColor & 0x000000ff);

							dataRed.SetElement(index, i * imageScaleSize + j, rgb[0]);
							dataGreen.SetElement(index, i * imageScaleSize + j, rgb[1]);
							dataBlue.SetElement(index, i * imageScaleSize + j, rgb[2]);
						}
					}

					//if (index % 10 == 0)
						//DisplayImage(index);
					//Console.WriteLine("Galaxy " + (sampleFactor * index) + " finished");
				});

				Console.WriteLine("Galaxy sampling finished\nBegin R, G and B channel SVD");
				DisplayMessage("Galaxy sampling finished, Begin R, G and B channel SVD");

				// Perform svd on subsample:
				var redWorker = System.Threading.Tasks.Task.Factory.StartNew(() => svdR = new SingularValueDecomposition(dataRed));
				var greenWorker = System.Threading.Tasks.Task.Factory.StartNew(() => svdG = new SingularValueDecomposition(dataGreen));
				var blueWorker = System.Threading.Tasks.Task.Factory.StartNew(() => svdB = new SingularValueDecomposition(dataBlue));
				System.Threading.Tasks.Task.WaitAll(redWorker, greenWorker, blueWorker);


				// Create the basis for each component
				GeneralMatrix rV = svdR.GetV();
				Console.Write("dim rU: " + rV.RowDimension + ", "
						+ rV.ColumnDimension);
				GeneralMatrix gV = svdG.GetV();
				GeneralMatrix bV = svdB.GetV();

				rV = GetSVs(rV, sv);
				Console.Write("Svs: " + sv);
				Console.Write("Dim SsV: " + rV.RowDimension + ", "
						+ rV.ColumnDimension);
				gV = GetSVs(gV, sv);
				bV = GetSVs(bV, sv);

				// Perform the pseudoinverses
				frV = pinv(rV.Transpose());
				fgV = pinv(gV.Transpose());
				fbV = pinv(bV.Transpose());

                // Stores frV, fgV and fbV to file
                WriteToFile();

				Console.WriteLine("SVD finished");
				DisplayMessage("SVD finished, load full dataset for testing");

				Console.WriteLine("Begin filling dataset for testing");

				// fill the 'full' datasets
				dataRed = new GeneralMatrix(galaxyData.Count(), imageScaleSize
						* imageScaleSize);
				dataGreen = new GeneralMatrix(galaxyData.Count(), imageScaleSize
						* imageScaleSize);
				dataBlue = new GeneralMatrix(galaxyData.Count(), imageScaleSize
						* imageScaleSize);

				System.Threading.Tasks.Parallel.For(0, galaxyData.Count(), (int index) => {
					Bitmap tempImage = getImage(OutputDir + "galaxies/"
							+ galaxyData[index][0] + ".jpg", imageScaleSize);

					for (int i = 0; i < imageScaleSize; i++) {
						for (int j = 0; j < imageScaleSize; j++) {
							int pixelColor = tempImage.GetPixel(i, j).ToArgb();
							int[] rgb = new int[3];
							rgb[0] += ((pixelColor & 0x00ff0000) >> 16);
							rgb[1] += ((pixelColor & 0x0000ff00) >> 8);
							rgb[2] += (pixelColor & 0x000000ff);

							dataRed.SetElement(index, i * imageScaleSize + j, rgb[0]);
							dataGreen.SetElement(index, i * imageScaleSize + j,
									rgb[1]);
							dataBlue.SetElement(index, i * imageScaleSize + j, rgb[2]);
						}
					}

				});

				Console.WriteLine("Finished filling dataset for testing");
				DisplayMessage("Finished filling dataset for testing, begin projecting galaxies to U coordinate system");

				Console.WriteLine("Begin projecting galaxies to U coordinate system, writing to ARFF file");

				// Do the coordinate conversion
				rV = dataRed.Multiply(frV);
				gV = dataGreen.Multiply(fgV);
				bV = dataBlue.Multiply(fbV);

				Console.Write("Dim Final rU: " + rV.ColumnDimension
						+ ", " + rV.RowDimension);
				Console.WriteLine("galaxyData.Count(): " + galaxyData.Count());

				// write to the output file here:
				for (int imageIndex = 0; imageIndex < galaxyData.Count(); imageIndex++) {

					Bitmap tempImage = getImage(OutputDir + "galaxies/"
							+ galaxyData[imageIndex][0] + ".jpg", imageScaleSize);

					float colorFactor = (GetColor(tempImage)[0] / GetColor(tempImage)[2]);
					float centralBulgeFactor = getCentralBulge(tempImage);
					float consistencyFactor = GetConsistency(tempImage);

					output.Write(galaxyData[imageIndex][1] + ", ");
					output.Write(colorFactor + ", ");
					output.Write(centralBulgeFactor + ", ");
					output.Write(consistencyFactor + ", ");

					// output data (r,g,b)
					for (int i = 0; i < rV.ColumnDimension; i++) {
						output.Write(rV.GetElement(imageIndex, i) + ", ");
						output.Write(gV.GetElement(imageIndex, i) + ", ");
						if (i == rV.ColumnDimension - 1) {
							output.Write(bV.GetElement(imageIndex, i) + "\n");
						}
						else {
							output.Write(bV.GetElement(imageIndex, i) + ", ");
						}
					}

                    //if (imageIndex % (galaxyData.Count() / 100) == 0)
                    //    DisplayImage(imageIndex);
					DisplayMessage("Finished galaxy " + imageIndex.ToString() + " - " + (100 * imageIndex / galaxyData.Count()).ToString() + "%");

				}

				output.Flush();
				output.Close();
				output.Dispose();

				Console.Write("Finished creating arff file...");
				DisplayMessage("Finished creating arff file...");

			}
			catch (Exception ex) {
				Console.Write(ex.ToString());
			}
		}

		private void fillGalaxyData() {
			try {
				Console.Write("Loading galaxy description data");
				DisplayMessage("Loading galaxy description data");

				int headerLength = 82;
				StreamReader input = new StreamReader(OutputDir + "galaxies/EFIGI_attributes.txt");

				// Find data length
				int dataLength = 0;
				while (!input.EndOfStream) {
					input.ReadLine();
					dataLength++;
				}
				dataLength -= headerLength;

				galaxyData = new String[dataLength][];
				for (int i = 0; i < dataLength; i++) {
					galaxyData[i] = new string[2];
				}


				input.Close();
				input.Dispose();

				// Restart at beginning now that we know size of data file, move
				// past header info
				input = new StreamReader(OutputDir + "galaxies/EFIGI_attributes.txt");
				for (int i = 0; i < headerLength; i++) {
					input.ReadLine();
				}
				for (int i = 0; i < galaxyData.Count(); i++) {
					String[] readLine = input.ReadLine().Split(' ');
					int temp = 1;
					while (readLine[temp].Equals("")) {
						temp++;
					}
					galaxyData[i][0] = readLine[0]; // Galaxy data file name
					galaxyData[i][1] = readLine[temp]; // Galaxy class
				}

				/*
				 * for(int i = 0; i < galaxyData.Count(); i++) {
				 * Console.Write(galaxyData[i][0] + ": " + galaxyData[i][1]); }
				 */

			}
			catch (Exception ex) {
				Console.Write("Error reading galaxy data file" + ex.ToString());
				DisplayMessage("Error reading galaxy data file " + ex.ToString());
			}
		}

		#endregion


		#region " Classification "

		public int classify(int index) {
			int classPrediction = (int)Math.Round(fc.classifyInstance(data.instance(index)));
			if (classPrediction < -6) {
				classPrediction = -6;
			}
			else if (classPrediction > 11) {
				classPrediction = 11;
			}

			return classPrediction;
		}

        private void ClassifyGroup(string[] images, string filePath, StreamWriter output, int groupNum) {
            // fill the 'full' datasets
            dataRed = new GeneralMatrix(images.Count(), imageScaleSize
                    * imageScaleSize);
			dataGreen = new GeneralMatrix(images.Count(), imageScaleSize
					* imageScaleSize);
			dataBlue = new GeneralMatrix(images.Count(), imageScaleSize
					* imageScaleSize);

			//create the entire size of the dataset
            System.Threading.Tasks.Parallel.For(0, images.Count(), (int imageIndex) => {
				Bitmap tempImage = getImage(images[imageIndex], imageScaleSize);

				for (int i = 0; i < imageScaleSize; i++) {
					for (int j = 0; j < imageScaleSize; j++) {
						int pixelColor = tempImage.GetPixel(i, j).ToArgb();
						int[] rgb = new int[3];
						rgb[0] += ((pixelColor & 0x00ff0000) >> 16);
						rgb[1] += ((pixelColor & 0x0000ff00) >> 8);
						rgb[2] += (pixelColor & 0x000000ff);

						dataRed.SetElement(imageIndex, i * imageScaleSize + j, rgb[0]);
						dataGreen.SetElement(imageIndex, i * imageScaleSize + j,
								rgb[1]);
						dataBlue.SetElement(imageIndex, i * imageScaleSize + j, rgb[2]);
					}
				}
                Console.WriteLine("SDSS Galaxy " + imageIndex + " put in main dataset");
            });

			//then convert the whole dataset to the same coordinate
			// Do the coordinate conversion
			GeneralMatrix rV = dataRed.Multiply(frV);
			GeneralMatrix gV = dataGreen.Multiply(fgV);
			GeneralMatrix bV = dataBlue.Multiply(fbV);

			Console.WriteLine("Dim Final rU: " + rV.ColumnDimension
					+ ", " + rV.RowDimension);
            Console.WriteLine("images.length: " + images.Count());

			// write to the output file here:
			for (int imageIndex = 0; imageIndex < images.Count(); imageIndex++) {

				Bitmap tempImage = getImage(images[imageIndex], imageScaleSize);

				float colorFactor = (GetColor(tempImage)[0] / GetColor(tempImage)[2]);
				float centralBulgeFactor = getCentralBulge(tempImage);
				float consistencyFactor = GetConsistency(tempImage);

				output.Write(-999 + ", ");

				output.Write(colorFactor + ", ");
				output.Write(centralBulgeFactor + ", ");
				output.Write(consistencyFactor + ", ");

				// output data (r,g,b)
				for (int i = 0; i < rV.ColumnDimension; i++) {
					output.Write(rV.GetElement(imageIndex, i) + ", ");
					output.Write(gV.GetElement(imageIndex, i) + ", ");
					if (i == rV.ColumnDimension - 1) {
						output.Write(bV.GetElement(imageIndex, i) + "\n");
					}
					else {
						output.Write(bV.GetElement(imageIndex, i) + ", ");
					}
				}

                Console.WriteLine("Creating ARFF classification file - " + (100 * imageIndex / images.Count()).ToString() + "%");
			}
        }

		// file path to a folder of galaxy images to classify
		public void classify(String filePath) {
            ThreadPool.QueueUserWorkItem((t) => {
                try {
                    Console.Write("Processing multiple images");
                    string[] images = System.IO.Directory.GetFiles(filePath).Where((string s) => {
                        return s.Trim().ToUpper().EndsWith(".JPG") || s.Trim().ToUpper().EndsWith(".PNG") || s.Trim().ToUpper().EndsWith(".PNG");
                    }).ToArray();

                    Console.Write("Num of galaxies to classify: " + images.Count());
                    DisplayMessage("Number of galaxies to classify: " + images.Count());

                    // sample the images for svd

                    // Create folder
                    System.IO.Directory.CreateDirectory(OutputDir + "Results/");
                    StreamWriter output = new StreamWriter(OutputDir + "Results/" + "temp.arff");

                    output.Write("@relation 'galaxy'\n");
                    output.Write("@attribute class real\n");
                    output.Write("@attribute colorF real\n");
                    output.Write("@attribute bulgeF real\n");
                    output.Write("@attribute constF real\n");
                    for (int i = 0; i < sv * 3; i++) {
                        output.Write("@attribute " + i + " real\n");
                    }
                    output.Write("@data\n");

                    int groupCount = (int)Math.Ceiling(((decimal)images.Count()) / (decimal)1000.0);

                    for (int i = 0; i < groupCount; i++) {
                        DisplayMessage("Classifying group " + (i + 1).ToString() + " of " + (groupCount).ToString() + " - " + (100 * i / groupCount).ToString() + "%");

                        int toTake = Math.Min(1000, images.Length - i * 1000);
                        ClassifyGroup(images.Skip(i * 1000).Take(toTake).ToArray(), filePath, output, i);
                    }

                    output.Flush();
                    output.Close();
                    output.Dispose();

                    Console.WriteLine("Finished creating testing arff file...");
                    DisplayMessage("Finished creating testing arff file...");

                    // create the list of image[] indices and their associated Equitorial Coords.
                    List<Datastructures.Coordinate> coords = new List<Datastructures.Coordinate>();

                    //parse the file names and intialize coords arraylist
                    for (int i = 0; i < images.Count(); i++) {
                        var matches = Regex.Matches(images[i].ToLower(), "[0-9]+(\\.[0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                        if (matches.Count > 0) {
                            Console.Write(images[i]);
                            Console.Write(matches[0].Groups[0].Value);
                            Console.WriteLine(matches[1].Groups[0].Value);
                            coords.Add(new Datastructures.Coordinate(Convert.ToDouble(matches[0].Groups[0].Value), Convert.ToDouble(matches[1].Groups[0].Value)));
                        }
                    }

                    Console.WriteLine("Start classifiying the images in " + filePath);
                    DisplayMessage("Start classifying the images in " + filePath);

                    StreamWriter outputClasses = new StreamWriter(OutputDir + "Results/" + "Final Classifications.txt");
                    outputClasses.Write("RA,	DEC,	Classification" + "\n");

                    weka.core.converters.ConverterUtils.DataSource source = new weka.core.converters.ConverterUtils.DataSource(OutputDir + "Results/temp.arff");
                    weka.core.Instances tests = source.getDataSet();
                    tests.setClassIndex(0);

                    for (int i = 0; i < tests.numInstances(); i++) {
                        int classPrediction = (int)Math.Round(fc.classifyInstance(tests.instance(i)));
                        if (classPrediction < -6) {
                            classPrediction = -6;
                        }
                        else if (classPrediction > 11) {
                            classPrediction = 11;
                        }
                        outputClasses.Write(coords[i].getRA() + ", " + coords[i].getDec() + ", " + classPrediction + "\n");
                        if (!System.IO.Directory.Exists(OutputDir + "Results/SampleOutput/"))
                            System.IO.Directory.CreateDirectory(OutputDir + "Results/SampleOutput/");
                        if (!System.IO.Directory.Exists(OutputDir + "Results/SampleOutput/Class" + (classPrediction.ToString() + "/")))
                            System.IO.Directory.CreateDirectory(OutputDir + "Results/SampleOutput/Class" + (classPrediction.ToString()) + "/");

                        new Bitmap(images[i]).Save(OutputDir + "Results/SampleOutput/Class" + classPrediction.ToString() + "/" + images[i].Substring(images[i].IndexOf('@')));
                    
                    }

                    outputClasses.Flush();
                    outputClasses.Close();
                    outputClasses.Dispose();

                    Console.WriteLine("Finished classifiying in " + filePath);
                    DisplayMessage("Finished classifying in " + filePath);
                }
                catch (Exception ex) {
                    Console.WriteLine(ex.ToString());
                }
            }, null);
		}

		public int classify(Bitmap currentImage) {
			try {
				currentImage.Save(OutputDir + "Results/temp.png", System.Drawing.Imaging.ImageFormat.Png);

				// Create folder
				System.IO.Directory.CreateDirectory(OutputDir + "Results/");
				StreamWriter output = new StreamWriter(OutputDir + "Results/" + "temp.arff");

				output.Write("@relation 'galaxy'\n");
				output.Write("@attribute class real\n");
				output.Write("@attribute colorF real\n");
				output.Write("@attribute bulgeF real\n");
				output.Write("@attribute constF real\n");
				for (int i = 0; i < sv * 3; i++) {
					output.Write("@attribute " + i + " real\n");
				}
				output.Write("@data\n");

				Bitmap tempImage = getImage(OutputDir + "Results/temp.png",
						imageScaleSize);

				for (int i = 0; i < imageScaleSize; i++) {
					for (int j = 0; j < imageScaleSize; j++) {
						int pixelColor = tempImage.GetPixel(i, j).ToArgb();
						int[] rgb = new int[3];
						rgb[0] += ((pixelColor & 0x00ff0000) >> 16);
						rgb[1] += ((pixelColor & 0x0000ff00) >> 8);
						rgb[2] += (pixelColor & 0x000000ff);

						dataRed.SetElement(galaxyData.Count() - 1, i * imageScaleSize + j,
								rgb[0]);
						dataGreen.SetElement(galaxyData.Count() - 1,
								i * imageScaleSize + j, rgb[1]);
						dataBlue.SetElement(galaxyData.Count() - 1, i * imageScaleSize + j,
								rgb[2]);
					}
				}

				var redWorker = System.Threading.Tasks.Task.Factory.StartNew(() => svdR = new SingularValueDecomposition(dataRed));
				var greenWorker = System.Threading.Tasks.Task.Factory.StartNew(() => svdG = new SingularValueDecomposition(dataGreen));
				var blueWorker = System.Threading.Tasks.Task.Factory.StartNew(() => svdB = new SingularValueDecomposition(dataBlue));
				System.Threading.Tasks.Task.WaitAll(redWorker, greenWorker, blueWorker);

				GeneralMatrix rU = svdR.GetU();
				GeneralMatrix gU = svdG.GetU();
				GeneralMatrix bU = svdB.GetU();
				rU = GetSVs(rU, sv);
				gU = GetSVs(gU, sv);
				bU = GetSVs(bU, sv);

				float colorFactor = (GetColor(tempImage)[0] / GetColor(tempImage)[2]); 
				float centralBulgeFactor = getCentralBulge(tempImage);
				float consistencyFactor = GetConsistency(tempImage);

				output.Write(galaxyData[galaxyData.Count() - 1][1] + ", ");
				output.Write(colorFactor + ", ");
				output.Write(centralBulgeFactor + ", ");
				output.Write(consistencyFactor + ", ");

				// output data (r,g,b)
				for (int i = 0; i < rU.ColumnDimension; i++) {
					output.Write(rU.GetElement(galaxyData.Count() - 1, i) + ", ");
					output.Write(gU.GetElement(galaxyData.Count() - 1, i) + ", ");
					if (i == rU.ColumnDimension - 1) {
						output.Write(bU.GetElement(galaxyData.Count() - 1, i) + "\n");
					}
					else {
						output.Write(bU.GetElement(galaxyData.Count() - 1, i) + ", ");
					}
				}

				output.Flush();
				output.Close();
				output.Dispose();

				weka.core.converters.ConverterUtils.DataSource source = new weka.core.converters.ConverterUtils.DataSource(OutputDir + "Results/temp.arff");
				weka.core.Instances test = source.getDataSet();
				test.setClassIndex(0);

				int classPrediction = (int)Math.Round(fc.classifyInstance(test.instance(0)));
				if (classPrediction < -6) {
					classPrediction = -6;
				}
				else if (classPrediction > 11) {
					classPrediction = 11;
				}

				return classPrediction;

			}
			catch (Exception ex) {
				Console.Write(ex.ToString());
			}

			return -99;
		}

		#endregion


		#region " Matrix Math "

		void PrintMatrix(GeneralMatrix matrix, StreamWriter output) {
			for (int j = 0; j < matrix.ColumnDimension; j++) {
				for (int i = 0; i < matrix.RowDimension; i++) {
					output.Write(matrix.GetElement(i, j) + ", ");
				}
				output.Write("\n");
			}
			output.Write("\n");
		}

		// Take SV columns from the U matrix
		private static GeneralMatrix GetSVs(GeneralMatrix matrix, int sv) {
			GeneralMatrix toPass = new GeneralMatrix(matrix.RowDimension, sv);
			for (int j = 0; j < sv; j++) {
				for (int i = 0; i < matrix.RowDimension; i++) {
					toPass.SetElement(i, j, matrix.GetElement(i, j));
				}
			}
			return toPass;
		}

		/**
		* Computes the Moore–Penrose pseudoinverse using the SVD method.
		* 
		* Modified version of the original implementation by Kim van der Linde.
		*/
		public static GeneralMatrix pinv(GeneralMatrix x) {
			if (x.Rank() < 1)
				return null;

			if (x.ColumnDimension > x.RowDimension)
				return pinv(x.Transpose()).Transpose();

			SingularValueDecomposition svdX = new SingularValueDecomposition(x);
			double[] singularValues = svdX.SingularValues;
			double tol = Math.Max(x.ColumnDimension, x.RowDimension)
					* singularValues[0] * 2E-16;

			double[] singularValueReciprocals = new double[singularValues.Count()];
			for (int i = 0; i < singularValues.Count(); i++)
				singularValueReciprocals[i] = Math.Abs(singularValues[i]) < tol ? 0
						: (1.0 / singularValues[i]);

			double[][] u = svdX.GetU().Array;
			double[][] v = svdX.GetV().Array;

			int min = Math.Min(x.ColumnDimension, u[0].Count());

			double[][] inverse = new double[x.ColumnDimension][];

			for (int i = 0; i < x.ColumnDimension; i++) {
				inverse[i] = new double[x.RowDimension];

				for (int j = 0; j < u.Count(); j++)
					for (int k = 0; k < min; k++)
						inverse[i][j] += v[i][k] * singularValueReciprocals[k] * u[j][k];
			}
			return new GeneralMatrix(inverse);
		}

		#endregion


		#region " Image Ops "

		public float[] GetColor(Bitmap img) {
			float[] rgb = new float[3];
			int imgWidth = img.Width;

			for (int x = 0; x < imgWidth; x++) {
				for (int y = 0; y < imgWidth; y++) {
					long pixelColor = img.GetPixel(x, y).ToArgb();
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

		public float GetConsistency(Bitmap img) {
			int imgWidth = img.Width;
			if (img.Width < imgWidth) {
				imgWidth = img.Height;
			}

			float[] consistencyArray = new float[imgWidth / 2];

			for (int i = 0; i < consistencyArray.Count(); i++) {
				consistencyArray[i] = 0;
			}

			for (int radius = 0; radius < consistencyArray.Count(); radius++) {
				for (float angle = 0.0f; angle < 2.0f * Math.PI; angle += (float)Math.PI / 180.0f) {
					int x = (int)(Math.Cos(angle) * radius + imgWidth / 2.0f);
					int y = (int)(Math.Sin(angle) * radius + imgWidth / 2.0f);

					long pixelColor = img.GetPixel(x, y).ToArgb();
					int red = (int)((pixelColor & 0x00ff0000) >> 16);
					int green = (int)((pixelColor & 0x0000ff00) >> 8);
					int blue = (int)(pixelColor & 0x000000ff);
					int brightness = (red + green + blue) / 3;

					consistencyArray[radius] += brightness * brightness
							/ consistencyArray.Count();
				}
			}

			for (int i = 0; i < consistencyArray.Count(); i++) {
				consistencyArray[i] = (float)Math.Sqrt(consistencyArray[i]);
			}

			for (int i = consistencyArray.Count() - 1; i > 0; i--) {
				consistencyArray[i] = (consistencyArray[i - 1] - consistencyArray[i]); 
			}

			float average = 0.0f;
			for (int i = 0; i < consistencyArray.Count(); i++) {
				average += consistencyArray[i]; // / (consistencyArray.Count * 2 *
				// Math.PI * i + 1);
			}
			average = average / (float)consistencyArray.Count();

			float stdDev = 0.0f;
			for (int i = 0; i < consistencyArray.Count(); i++) {
				stdDev += (average - consistencyArray[i])
						* (average - consistencyArray[i]) / (256.0f * 256.0f);
			}
			stdDev = (float)Math.Sqrt(stdDev / (float)consistencyArray.Count());

			if (Math.Abs(average) < 1) { // Avoid division by 0
				average = 1;
			}

			// Console.Write(stdDev + ", " + average);

			return stdDev;
		}

		public float getImageRotationAngle(Bitmap img) {
			float widestAngle = 0.0f;
			int imgWidth = img.Width;
			if (img.Height < imgWidth) {
				imgWidth = img.Height;
			}
			int avgBrightness = 0;

			for (int x = 0; x < imgWidth; x++) {
				for (int y = 0; y < imgWidth; y++) {
					long pixelColor = img.GetPixel(x, y).ToArgb();
					int red = (int)((pixelColor & 0x00ff0000) >> 16);
					int green = (int)((pixelColor & 0x0000ff00) >> 8);
					int blue = (int)(pixelColor & 0x000000ff);
					avgBrightness += (red + green + blue) / 3;
				}
			}
			avgBrightness = avgBrightness / (imgWidth * imgWidth);

			int brightnessOfRadius = 0;
			int bestRadialBrightness = 0;

			for (float angle = 0.0f; angle < 2.0f * Math.PI; angle += (float)Math.PI / 180.0f) {
				for (float radius = -imgWidth / 2.0f; radius < imgWidth / 2.0f; radius++) {
					int x = (int)(Math.Cos(angle) * radius + imgWidth / 2.0f);
					int y = (int)(Math.Sin(angle) * radius + imgWidth / 2.0f);

					long pixelColor = img.GetPixel(x, y).ToArgb();
					int red = (int)((pixelColor & 0x00ff0000) >> 16);
					int green = (int)((pixelColor & 0x0000ff00) >> 8);
					int blue = (int)(pixelColor & 0x000000ff);
					int brightness = (red + green + blue) / 3;

					brightnessOfRadius += brightness;
				}
				if (brightnessOfRadius > bestRadialBrightness) {
					bestRadialBrightness = brightnessOfRadius;
					widestAngle = angle;
				}
				brightnessOfRadius = 0;
			}

			return -180.0f * widestAngle / ((float)Math.PI);
		}

		public float getCentralBulge(Bitmap img) {
			int w = img.Width;
			int h = img.Height;
			float lastAvg = 0;
			float peakDecline = 0.0f;
			float peakDeclineCount = 0.0f;

			for (float radius = 1; radius < w / 2; radius++) {
				float radialAvg = 0.0f;
				for (float angle = 0.0f; angle < 2.0f * Math.PI; angle += (float)Math.PI / 45.0f) {
					int x = (int)(Math.Cos(angle) * radius) + w / 2;
					int y = (int)(Math.Sin(angle) * radius) + h / 2;

					long pixelColor = img.GetPixel(x, y).ToArgb();
					int red = (int)((pixelColor & 0x00ff0000) >> 16);
					int green = (int)((pixelColor & 0x0000ff00) >> 8);
					int blue = (int)(pixelColor & 0x000000ff);
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

		public float getChirality(Bitmap image) {
			float chirality = 0f;
			int width = image.Width;
			int height = image.Height;
			float angleSum = 0f;
			float currentX = width / 2, currentY = height / 2;
			float radDistance = 0.1f; // 0.2f?
			float lastAngle = 0.0f;

			for (float i = 0.0f; i < width * radDistance; i++) {
				float brightnessSum = 0f, brightestSum = 0f, brightestAngle = 0f;

				for (float angle = -(float)Math.PI / 2.0f + lastAngle; angle < Math.PI
						/ 2.0f + lastAngle; angle += (float)Math.PI / 180.0f) {
					brightnessSum = 0;

					for (float radius = 1; radius < width * radDistance; radius++) {
						int x = (int)(Math.Cos(angle) * radius + currentX);
						int y = (int)(Math.Sin(angle) * radius + currentY);

						if (x > width - 1) {
							x = width - 1;
							i = width * radDistance;
							angle = (float)Math.PI / 2.0f + lastAngle;
							radius = width * radDistance;
						}
						else if (x < 0) {
							x = 0;
							i = width * radDistance;
							angle = (float)Math.PI / 2.0f + lastAngle;
							radius = width * radDistance;
						}

						if (y > height - 1) {
							y = height - 1;
							i = width * radDistance;
							angle = (float)Math.PI / 2.0f + lastAngle;
							radius = width * radDistance;
						}
						else if (y < 0) {
							y = 0;
							i = width * radDistance;
							angle = (float)Math.PI / 2.0f + lastAngle;
							radius = width * radDistance;
						}

						long pixelColor = image.GetPixel(x, y).ToArgb();
						int red = (int)((pixelColor & 0x00ff0000) >> 16);
						int green = (int)((pixelColor & 0x0000ff00) >> 8);
						int blue = (int)(pixelColor & 0x000000ff);
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
				currentX += (int)(Math.Cos(brightestAngle) * width * (0.15));
				currentY += (int)(Math.Sin(brightestAngle) * width * (0.15));
			}
			chirality = (angleSum * radDistance) / width;

			return chirality;
		}

		public Bitmap stretchImage(Bitmap img) {
			int w = img.Width;
			int h = img.Height;

			int dimmestYValue = 255;
			int dimmestXValue = 255;

			for (int i = 0; i < w / 2; i++) {
				int pixelColor = img.GetPixel(w / 2 + i, h / 2).ToArgb();
				int red = (int)((pixelColor & 0x00ff0000) >> 16);
				int green = (int)((pixelColor & 0x0000ff00) >> 8);
				int blue = (int)(pixelColor & 0x000000ff);
				int color = (red + green + blue) / 3;
				if (color < dimmestXValue) {
					dimmestXValue = color;
				}
			}

			for (int i = 0; i < h / 2; i++) {
				int pixelColor = img.GetPixel(w / 2, h / 2 + i).ToArgb();
				int red = (int)((pixelColor & 0x00ff0000) >> 16);
				int green = (int)((pixelColor & 0x0000ff00) >> 8);
				int blue = (int)(pixelColor & 0x000000ff);
				int color = (red + green + blue) / 3;
				if (color < dimmestYValue) {
					dimmestYValue = color;
				}
			}

			int currentBrightness = 255;
			int actualGalaxyWidth = 0;
			while (currentBrightness > 2 * dimmestXValue
					&& actualGalaxyWidth < w / 2) {
				int pixelColor = img.GetPixel(w / 2 + actualGalaxyWidth, h / 2).ToArgb();
				int red = (int)((pixelColor & 0x00ff0000) >> 16);
				int green = (int)((pixelColor & 0x0000ff00) >> 8);
				int blue = (int)(pixelColor & 0x000000ff);
				currentBrightness = (red + green + blue) / 3;

				actualGalaxyWidth++;
			}

			currentBrightness = 255;
			int actualGalaxyHeight = 0;
			while (currentBrightness > 2 * dimmestYValue
					&& actualGalaxyHeight < h / 2) {
				int pixelColor = img.GetPixel(w / 2, h / 2 + actualGalaxyHeight).ToArgb();
				int red = (int)((pixelColor & 0x00ff0000) >> 16);
				int green = (int)((pixelColor & 0x0000ff00) >> 8);
				int blue = (int)(pixelColor & 0x000000ff);
				currentBrightness = (red + green + blue) / 3;

				actualGalaxyHeight++;
			}

            if (actualGalaxyWidth < 10)
                actualGalaxyWidth = 10;
            if (actualGalaxyHeight < 10)
                actualGalaxyHeight = 10;

			float imgRatio = (float)actualGalaxyWidth / (float)actualGalaxyHeight;
				Bitmap result = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
				Graphics g = Graphics.FromImage(result);
				//set the InterpolationMode to HighQualityBicubic to ensure a high
				//quality image once it is transformed to the specified size
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, new System.Drawing.Rectangle(0, 0, w, h),
                    new System.Drawing.Rectangle((w / 2) - (actualGalaxyWidth / 2), (int)(((h / 2) - (actualGalaxyHeight / 2)) * (1.0f / imgRatio + 1.0f) / 2.0f),
                    actualGalaxyWidth, (int)(actualGalaxyHeight * (1.0f / imgRatio + 1.0f) / 2.0f)), GraphicsUnit.Pixel);

				g.Flush();
				g.Dispose();

				return result;
		}

        // Black out area outside central galaxy
        private Bitmap BlackInvalidArea(Bitmap src) {
            float currBrightness = 0.0f;
            float avgBrightness = 0.0f;
            int widthToTake = 0;
            int countBelowAvg = 0;
            for (widthToTake = 0; widthToTake < src.Width / 2
                                    && (countBelowAvg < widthToTake / 2 || widthToTake < src.Width * 0.05); widthToTake++) {

                currBrightness = 0.0f;
                int pixBelowAvg = 0;
                for (float angle = 0; angle < 2.0 * Math.PI; angle += (float)Math.PI / 180.0f) {
                    int x = (int)(Math.Cos(angle) * widthToTake) + src.Width / 2;
                    int y = (int)(Math.Sin(angle) * widthToTake) + src.Height / 2;

                    long pixelColor = src.GetPixel(x, y).ToArgb();
                    int red = (int)((pixelColor & 0x00ff0000) >> 16);
                    int green = (int)((pixelColor & 0x0000ff00) >> 8);
                    int blue = (int)(pixelColor & 0x000000ff);
                    int pixBrightness = (red + green + blue) / 3;
                    if (pixBrightness < avgBrightness / 2)
                        pixBelowAvg++;

                    currBrightness += pixBrightness;
                }
                currBrightness = currBrightness / 360.0f;

                if (currBrightness < avgBrightness / 2 && pixBelowAvg > 225 && widthToTake > (float)src.Width * 0.05)
                    countBelowAvg++;

                avgBrightness = (avgBrightness * widthToTake + currBrightness) / (widthToTake + 1);
            }

            Graphics srcContext = Graphics.FromImage(src);
            Pen blackPen = new System.Drawing.Pen(Color.Black);
            Point center = new Point(src.Width / 2, src.Height / 2);
            for (int i = 2 * widthToTake; i < src.Width; i++) {
                srcContext.DrawEllipse(blackPen, center.X - i / 2, center.Y - i / 2, i, i);
            }
            
            srcContext.Flush();

            return src;
        }

		public Bitmap getImage(String fileName, int scaledResolution) {
			Bitmap source = null;
			Bitmap result = null;

			try {
				// Read the source image file and scale with temp image, store back
				// to source
				source = new Bitmap(Image.FromFile(fileName));

                BlackInvalidArea(source);

				result = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

				Graphics tempContext = Graphics.FromImage(result);
				//set the InterpolationMode to HighQualityBicubic to ensure a high
				//quality image once it is transformed
				tempContext.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				tempContext.Clear(Color.Black);

				// Setup rotation
				float rotationAngle = getImageRotationAngle(source);
				System.Drawing.Drawing2D.Matrix rotateMatrix = new System.Drawing.Drawing2D.Matrix();
				rotateMatrix.RotateAt(rotationAngle, new Point(result.Width / 2, result.Height / 2));
				tempContext.Transform = rotateMatrix;

				tempContext.DrawImage(source, 0, 0);
                tempContext.Flush();
				tempContext.Dispose();

				float chirality = getChirality(source);

				if (chirality < 0) {
					// flip the image
					int w = result.Width;
					int h = result.Height;
					source = new Bitmap(scaledResolution, scaledResolution, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
					Graphics resultContext = Graphics.FromImage(source);
					resultContext.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					//Flip image horizontally
					result.RotateFlip(RotateFlipType.RotateNoneFlipX);
					resultContext.DrawImage(result, 0, 0, scaledResolution, scaledResolution);
				}
				else {
					source = new Bitmap(scaledResolution, scaledResolution, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
					Graphics resultContext = Graphics.FromImage(source);
					resultContext.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					resultContext.DrawImage(result, 0, 0, scaledResolution, scaledResolution);
				}
			}
			catch (Exception ex) {
				Console.Write("Failed to open: " + fileName + ", "
						+ ex.ToString());
			}
			return source;
		}

		#endregion

	}
	 
}
