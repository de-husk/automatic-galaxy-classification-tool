using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotNetMatrix;

namespace NAudioFormsTest1
{
    class Decomposition
    {
        public int sv;
        public int frameScale;
        GeneralMatrix squareM;
        GeneralMatrix recomposition;

        public double[] decompose(int svCount, double[] m)
        {
            GeneralMatrix mMat = new GeneralMatrix(m, (int)Math.Sqrt(m.Length));
            GeneralMatrix[] resultM = decompose(svCount, mMat);
            double[] resultD = new double[resultM[0].RowDimension * resultM[0].ColumnDimension + resultM[1].RowDimension * resultM[1].ColumnDimension];
            for (int i = 0; i < resultM[0].RowDimension; i++)
            {
                for (int j = 0; j < resultM[0].ColumnDimension; j++)
                {
                    resultD[i * resultM[0].ColumnDimension + j] = resultM[0].GetElement(i, j);
                }
            }

            for (int i = 0; i < resultM[1].RowDimension; i++)
            {
                for (int j = 0; j < resultM[1].ColumnDimension; j++)
                {
                    resultD[resultM[0].RowDimension * resultM[0].ColumnDimension
                         + i * resultM[1].ColumnDimension + j] = resultM[1].GetElement(i, j);
                }
            }

            return resultD;
        }

        public GeneralMatrix[] decompose(int svCount, GeneralMatrix m)
        {
            SingularValueDecomposition s = new SingularValueDecomposition(m);
            try
            {
                sv = svCount;
                frameScale = m.RowDimension;

                if (m.RowDimension > m.ColumnDimension)
                {
                    frameScale = m.ColumnDimension;
                }
                else if (m.RowDimension < m.ColumnDimension)
                {
                    frameScale = m.RowDimension;
                }

                // Make square matrix of data
                if (m.RowDimension != m.ColumnDimension)
                {
                    squareM = m.GetMatrix(0, frameScale - 1, 0, frameScale - 1);
                }
                else
                {
                    squareM = m;
                }

                //perform the SVD here:
                SingularValueDecomposition svd = new SingularValueDecomposition(squareM);

                GeneralMatrix U = svd.GetU().GetMatrix(0, frameScale - 1, 0, sv - 1);
                GeneralMatrix V = svd.GetV();
                double[] D = svd.SingularValues;

                GeneralMatrix dMat = makeDiagonalSquare(D, sv);
                GeneralMatrix vMat = V.Transpose().GetMatrix(0, sv - 1, 0, frameScale - 1);

                /*Console.WriteLine(U.RowDimension + "x" + U.ColumnDimension
                        + " * " + dMat.RowDimension + "x" + dMat.ColumnDimension
                        + " * " + vMat.RowDimension + "x" + vMat.ColumnDimension);

                recomposition = U.Multiply(dMat).Multiply(vMat);

                int[,] recompositionData = new int[recomposition.ColumnDimension, recomposition.RowDimension];

                for (int i = 0; i < recomposition.ColumnDimension; i++)
                {
                    for (int j = 0; j < recomposition.RowDimension; j++)
                    {
                        recompositionData[j, i] = (int)(recomposition.GetElement(j, i));
                    }
                }*/

                GeneralMatrix[] result = new GeneralMatrix[2];
                result[0] = U;
                result[1] = vMat;
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return null;
        }

        private static GeneralMatrix makeDiagonalSquare(double[] m, int cols)
        {
            GeneralMatrix result = new GeneralMatrix(cols, cols);

            for (int j = 0; j < cols; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    if (j == i)
                    {
                        result.SetElement(i, j, m[i]);
                    }
                    else
                    {
                        result.SetElement(i, j, 0);
                    }
                }
            }
            return result;
        }
    }
}
