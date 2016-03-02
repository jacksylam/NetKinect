//----------------------------------------------------------------------------
//  Copyright (C) 2004-2016 by EMGU Corporation. All rights reserved.       
//----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Diagnostics;
using Emgu.CV.Util;

namespace NetKinect
{
   public partial class MainForm : Form
   {
      public MainForm()
      {
         InitializeComponent();

         fileNameTextBox.Text = "balltest.png";
      }

      public void PerformShapeDetection()
      {
         if (fileNameTextBox.Text != String.Empty)
         {
            StringBuilder msgBuilder = new StringBuilder("Performance: ");

            //Load the image from file and resize it for display
            Image<Bgr, Byte> img = 
               new Image<Bgr, byte>(fileNameTextBox.Text)
               .Resize(400, 400, Emgu.CV.CvEnum.Inter.Linear, true);

            //Convert the image to grayscale and filter out the noise
            UMat uimage = new UMat();
            CvInvoke.CvtColor(img, uimage, ColorConversion.Bgr2Gray);

            //use image pyr to remove noise
            UMat pyrDown = new UMat();
            CvInvoke.PyrDown(uimage, pyrDown);
            CvInvoke.PyrUp(pyrDown, uimage);
            
            //Image<Gray, Byte> gray = img.Convert<Gray, Byte>().PyrDown().PyrUp();

            #region circle detection
            Stopwatch watch = Stopwatch.StartNew();
           // double cannyThreshold = 180.0;
            double cannyThreshold = 100;

            //double circleAccumulatorThreshold = 120;
            double circleAccumulatorThreshold = 10;

            UMat invertImage = new UMat();

            CvInvoke.BitwiseNot(uimage, invertImage);
            CircleF[] circles = CvInvoke.HoughCircles(invertImage, HoughType.Gradient, 1, 5.0, cannyThreshold, circleAccumulatorThreshold, 1, 0);

            Debug.Print(circles.Length.ToString());
            watch.Stop();
            msgBuilder.Append(String.Format("Hough circles - {0} ms; ", watch.ElapsedMilliseconds));
            msgBuilder.Append(String.Format("Number of circles: {0}", circles.Length));
            #endregion


            originalImageBox.Image = img;
            this.Text = msgBuilder.ToString();

            #region InvertImage
            triangleRectangleImageBox.Image = invertImage;
            #endregion

            #region draw circles
            Mat circleImage = new Mat(img.Size, DepthType.Cv8U, 3);
            circleImage.SetTo(new MCvScalar(0));
            foreach (CircleF circle in circles)
               CvInvoke.Circle(circleImage, Point.Round(circle.Center), (int) circle.Radius, new Bgr(Color.Brown).MCvScalar, 2);
               
            circleImageBox.Image = circleImage;
            #endregion

         }
      }

      private void textBox1_TextChanged(object sender, EventArgs e)
      {
         PerformShapeDetection();
      }

      private void loadImageButton_Click(object sender, EventArgs e)
      {
         DialogResult result = openFileDialog1.ShowDialog();
         if (result == DialogResult.OK || result == DialogResult.Yes)
         {
            fileNameTextBox.Text = openFileDialog1.FileName;
         }
      }
   }
}
