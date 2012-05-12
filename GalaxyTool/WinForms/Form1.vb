Imports ClassificationService
Imports System.IO
Imports System.Threading

Public Class Form1
    ' /*
    ' * GalaxyToolForm
    ' *
    ' * Created on Nov 30, 2011, 11:28:11 PM
    ' */

    '/**
    ' * @author Harry Hull, Craig Williams
    ' * UALR Department of Computer Science
    ' * 2012-4-28
    ' * 
    ' * This is a tool to automate preprocessing of galaxy images.
    ' */

#Region " Form Properties "

    Private WithEvents Logic As ClassificationService.Logic = New ClassificationService.Logic()
    Private WithEvents ServiceAgent As New Classify()

    Private MainSyncContext As SynchronizationContext = SynchronizationContext.Current

    Private Shared currentImage As Bitmap
    Private Shared lastX As Integer = 0, lastY As Integer = 0
    Private Shared rotation As Double = 0.0F
    Private Shared lastSpinnerValue As Integer = 0

    Private StoringImage As Boolean = False
    Private SyncObject As New Object

    Private Shared galaxyFiles As Dictionary(Of String, Integer) '// First dimension: file name
    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''// Second dimension: file classification

#End Region

    Private Sub DisplayMessage(ByVal message As String) Handles Logic.DisplayMessage
        'ThreadPool.QueueUserWorkItem(Sub()
        MainSyncContext.Post(Sub() Me.lblDisplay.Text = message, message)
        'End Sub)
    End Sub

    Private Sub setImageIndex(index As Integer) Handles Logic.DisplayImage
        SyncLock SyncObject
            If Not StoringImage Then
                StoringImage = True

                MainSyncContext.Post(Sub()
                                         Dim cHeight As Integer = PictureBox1.Height

                                         currentImage = Logic.getImage("../../../galaxies/" + galaxyFiles.Keys(index) + ".jpg", cHeight)
                                         Dim colorRatio = Logic.GetColor(currentImage)(0) / Logic.GetColor(currentImage)(2)
                                         Me.PictureBox1.Image = currentImage

                                         If Not ServiceAgent.running Then

                                             Dim predictedClass = Logic.classify(CInt(Me.NumericUpDown1.Value))
                                             Me.lblClassification.Text = "Classification: " + predictedClass.ToString() _
                                                                             + ", Error: " _
                                                                             + (predictedClass - galaxyFiles.Values.ElementAt(CInt(Me.NumericUpDown1.Value))).ToString
                                             Me.lblInfo.Text = "Image: " + galaxyFiles.Keys(index) + vbNewLine + vbNewLine _
                                                 + "Color ratio: " + colorRatio.ToString + "   " _
                                                 + "Central bulge: " + Logic.getCentralBulge(currentImage).ToString + "   " _
                                                 + "Structure factor: " + Logic.GetConsistency(currentImage).ToString + vbNewLine + vbNewLine
                                         End If
                                     End Sub, Nothing)

                StoringImage = False
            End If
        End SyncLock
    End Sub

    Private Sub Form1_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown
        ThreadPool.QueueUserWorkItem(Sub()
                                         FillGalaxyDescriptions()
                                         ServiceAgent.run(Logic)
                                     End Sub)
    End Sub

    Private Sub FinishedProcessing() Handles ServiceAgent.FinishedProcessing
        MainSyncContext.Post(Sub()
                                 Me.NumericUpDown1.Enabled = True
                                 Me.NumericUpDown1.Maximum = galaxyFiles.Count()
                                 Me.lblInfo.Text = "RMSE: " + Logic.rmse.ToString
                                 setImageIndex(0)
                             End Sub, Nothing)
    End Sub

    Private Shared Sub FillGalaxyDescriptions()
        Try
            Dim headerLength As Integer = 82
            Dim input As StreamReader = New StreamReader("../../../galaxies/EFIGI_attributes.txt")

            '// Find data length
            Dim dataLength As Integer = 0
            While Not input.EndOfStream
                input.ReadLine()
                dataLength += 1
            End While
            dataLength -= headerLength
            galaxyFiles = New Dictionary(Of String, Integer)
            input.Close()

            '// Restart at beginning now that we know size of data file, move
            '// past header info
            input = New StreamReader("../../../galaxies/EFIGI_attributes.txt")
            For i = 0 To headerLength - 1
                input.ReadLine()
            Next

            For i = 0 To dataLength - 1
                Dim readLine As String() = input.ReadLine().Split(" "c)

                Dim temp = 1
                While readLine(temp).Equals("")
                    temp += 1
                End While

                Dim actualClass As Integer = -99
                Integer.TryParse(readLine(temp), actualClass)
                galaxyFiles.Add(readLine(0), actualClass) '// Galaxy data file name, galaxy actual classification
            Next

            '/*
            ' * for(int i = 0; i < galaxyData.length; i++) {
            ' * System.out.println(galaxyData[i][0] + ": " + galaxyData[i][1]); }
            ' */
        Catch ex As Exception
            MessageBox.Show("Error reading galaxy data file")
        End Try
    End Sub

    Private Sub NumericUpDown1_ValueChanged(sender As Object, e As System.EventArgs) Handles NumericUpDown1.ValueChanged
        setImageIndex(CInt(Me.NumericUpDown1.Value))
    End Sub

    Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles Button1.Click
        If Logic IsNot Nothing AndAlso Logic.readyToClassify Then
            Dim f As String = ""
            Dim fbd = New FolderBrowserDialog
            fbd.SelectedPath = "../../../galaxies/"
            fbd.ShowNewFolderButton = False
            fbd.Description = "Please select a folder containing the galaxy images you wish to classify."
            If fbd.ShowDialog() = Windows.Forms.DialogResult.OK Then
                Logic.classify(fbd.SelectedPath)
            End If
        End If
    End Sub
End Class

Class Classify
    Friend running As Boolean = False

    Public Event FinishedProcessing()

    Public Sub run(ByVal Logic As ClassificationService.Logic)
        running = True

        If MessageBox.Show("Load classifier?", "Load?", MessageBoxButtons.YesNo) = DialogResult.Yes Then
            ThreadPool.QueueUserWorkItem(Sub()
                                             Try
                                                 Logic.train(True)
                                                 running = False
                                                 RaiseEvent FinishedProcessing()
                                             Catch ex As Exception
                                                 Debug.WriteLine(ex.ToString)
                                                 If MessageBox.Show("Failed to load classifier, create and train new one?", "Failure", MessageBoxButtons.YesNo) = DialogResult.Yes Then
                                                     Logic.run()
                                                     running = False
                                                     RaiseEvent FinishedProcessing()
                                                 End If
                                             End Try
                                         End Sub)
        Else
            Console.WriteLine("Begin classification of galaxies...")

            Try
                ThreadPool.QueueUserWorkItem(Sub()
                                                 Logic.run()
                                                 running = False
                                                 RaiseEvent FinishedProcessing()
                                             End Sub)
            Catch ex As Exception
                Console.WriteLine(ex.ToString())
                MessageBox.Show("Failure classifying galaxies")
            End Try

            Console.WriteLine("End classification of current galaxy")
        End If

    End Sub

End Class