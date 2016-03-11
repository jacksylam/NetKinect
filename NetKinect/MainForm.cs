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
using Microsoft;
using Microsoft.Kinect;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace NetKinect
{
    public enum DisplayFrameType {
        Infrared,
        Color,
        Depth,
        BodyMask,
        BodyJoints,
        Debug
    }

   public partial class MainForm : Form
   {

       private const DisplayFrameType DEFAULT_DISPLAYFRAMETYPE = DisplayFrameType.Infrared;
       private const float InfraredSourceValueMaximum = (float)ushort.MaxValue;
       private const float InfraredOutputValueMinimum = 0.01f;
       private const float InfraredOutputValueMaximum = 1.0f;
       private const float InfraredSceneValueAverage = 0.08f;
       private const float InfraredSceneStandardDeviations = 3.0f;

       // Size of the RGB pixel in the bitmap
       private const int BytesPerPixel = 4;

       private KinectSensor kinectSensor = null;
       private string statusText = null;
       private FrameDescription currentFrameDescription;
       private DisplayFrameType currentDisplayFrameType;
       private MultiSourceFrameReader multiSourceFrameReader = null;
       private CoordinateMapper coordinateMapper = null;
       private WriteableBitmap bitmap = null; 

       //Infrared Frame 
       private ushort[] infraredFrameData = null;
       private byte[] infraredPixels = null;

       //Depth Frame
       private ushort[] depthFrameData = null;
       private byte[] depthPixels = null;

       private int infraredWidth;
       private int infraredHeight;

      public MainForm()
      {
         InitializeComponent();
         fileNameTextBox.Text = "balltest.png";
         // one sensor is currently supported
         this.kinectSensor  = KinectSensor.GetDefault();

         SetupCurrentDisplay(DEFAULT_DISPLAYFRAMETYPE);

         this.coordinateMapper = this.kinectSensor.CoordinateMapper;

         // this.multiSourceFrameReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Infrared | FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.BodyIndex | FrameSourceTypes.Body);


         this.multiSourceFrameReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Infrared | FrameSourceTypes.Depth | FrameSourceTypes.BodyIndex | FrameSourceTypes.Body);


       //  this.multiSourceFrameReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;

         // set IsAvailableChanged event notifier
         //this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;
         // open the sensor
         this.kinectSensor.Open();



      }

      private void SetupCurrentDisplay(DisplayFrameType newDisplayFrameType) {
          currentDisplayFrameType = newDisplayFrameType;
          switch (currentDisplayFrameType) {
              case DisplayFrameType.Infrared:
                  FrameDescription infraredFrameDescription = this.kinectSensor.InfraredFrameSource.FrameDescription;
                  this.infraredFrameData = new ushort[infraredFrameDescription.Width * infraredFrameDescription.Height];
                  this.infraredPixels = new byte[infraredFrameDescription.Width * infraredFrameDescription.Height * BytesPerPixel];
                  this.bitmap = new WriteableBitmap(infraredFrameDescription.Width, infraredFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray32Float, null);
                  break;
              
              case DisplayFrameType.Depth:
                  FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
                  this.depthFrameData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height];
                  this.depthPixels = new byte[depthFrameDescription.Width * depthFrameDescription.Height * BytesPerPixel];
                  this.bitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray32Float, null);
                  break;
              default:
                  break;
          }
      }

      private void Reader_MultiSourceFrameArrived(MultiSourceFrameReader sender, MultiSourceFrameArrivedEventArgs e) {

          MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

          // If the Frame has expired by the time we process this event, return.
          if (multiSourceFrame == null) {
              return;
          }
          DepthFrame depthFrame = null;
          ColorFrame colorFrame = null;
          InfraredFrame infraredFrame = null;
          BodyFrame bodyFrame = null;
          BodyIndexFrame bodyIndexFrame = null;


          switch (currentDisplayFrameType) {
              case DisplayFrameType.Infrared:
                  using (infraredFrame = multiSourceFrame.InfraredFrameReference.AcquireFrame())
                  using (bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame()) {
                      ShowInfraredFrame(infraredFrame, bodyFrame);
                  }
                  break;
              case DisplayFrameType.Depth:
                  using (depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame()) {
                      ShowDepthFrame(depthFrame);
                  }
                  break;
              default:
                  break;
          }
      }

      private void ShowDepthFrame(DepthFrame depthFrame) {
          bool depthFrameProcessed = false;
          ushort minDepth = 0;
          ushort maxDepth = 0;

          if (depthFrame != null) {
              FrameDescription depthFrameDescription = depthFrame.FrameDescription;

              // verify data and write the new infrared frame data to the display bitmap
              if (((depthFrameDescription.Width * depthFrameDescription.Height)
                  == this.infraredFrameData.Length) &&
                  (depthFrameDescription.Width == this.bitmap.Width) &&
                  (depthFrameDescription.Height == this.bitmap.Height)) {
                  // Copy the pixel data from the image to a temporary array
                  depthFrame.CopyFrameDataToArray(this.depthFrameData);

                  minDepth = depthFrame.DepthMinReliableDistance;
                  maxDepth = depthFrame.DepthMaxReliableDistance;
                  //maxDepth = 8000;

                  depthFrameProcessed = true;
              }
          }

          // we got a frame, convert and render
          if (depthFrameProcessed) {
             // ConvertDepthDataToPixels(minDepth, maxDepth);
              RenderPixelArray(this.depthPixels);
          }
      }

      private void ShowInfraredFrame(InfraredFrame infraredFrame, BodyFrame bodyFrame) {
          bool infraredFrameProcessed = false;

          if (infraredFrame != null) {
              FrameDescription infraredFrameDescription = infraredFrame.FrameDescription;

              // verify data and write the new infrared frame data to the display bitmap
              if (((infraredFrameDescription.Width * infraredFrameDescription.Height)
                  == this.infraredFrameData.Length) &&
                  (infraredFrameDescription.Width == this.bitmap.Width) &&
                  (infraredFrameDescription.Height == this.bitmap.Height)) {

                  //Debug.WriteLine("Width is " + infraredFrameDescription.Width);
                  //Debug.WriteLine("Height is " + infraredFrameDescription.Height);

                  infraredWidth = infraredFrameDescription.Width;
                  infraredHeight = infraredFrameDescription.Height;

                  // Copy the pixel data from the image to a temporary array
                  infraredFrame.CopyFrameDataToArray(this.infraredFrameData);

                  infraredFrameProcessed = true;
              }
          }
      }

      private void ConvertInfraredDataToPixels(Body[] myBodies) {
          // Convert the infrared to RGB
          int colorPixelIndex = 0;
          for (int i = 0; i < this.infraredFrameData.Length; ++i) {
              // normalize the incoming infrared data (ushort) to a float ranging from 
              // [InfraredOutputValueMinimum, InfraredOutputValueMaximum] by
              // 1. dividing the incoming value by the source maximum value
              float intensityRatio = (float)this.infraredFrameData[i] / InfraredSourceValueMaximum;

              // 2. dividing by the (average scene value * standard deviations)
              intensityRatio /= InfraredSceneValueAverage * InfraredSceneStandardDeviations;

              // 3. limiting the value to InfraredOutputValueMaximum
              intensityRatio = Math.Min(InfraredOutputValueMaximum, intensityRatio);

              // 4. limiting the lower value InfraredOutputValueMinimum
              intensityRatio = Math.Max(InfraredOutputValueMinimum, intensityRatio);

              // 5. converting the normalized value to a byte and using the result
              // as the RGB components required by the image
              byte intensity = (byte)(intensityRatio * 255.0f);
              if (intensity > 254) {
                  bool isRetroReflexiveBall = false;
                  foreach (Body body in myBodies) {
                      //get the left and right hand joints
                      Joint rightHand = body.Joints[JointType.HandRight];
                      Joint leftHand = body.Joints[JointType.HandLeft];

                      Joint spineMid = body.Joints[JointType.SpineMid];
                      Joint spineBase = body.Joints[JointType.SpineBase];

                      //Convert Camera space (for body) to Depth space (for Innfrared)
                      DepthSpacePoint rightPoint = this.kinectSensor.CoordinateMapper.MapCameraPointToDepthSpace(rightHand.Position);
                      DepthSpacePoint leftPoint = this.kinectSensor.CoordinateMapper.MapCameraPointToDepthSpace(leftHand.Position);
                      int rightHandX = (int)rightPoint.X;
                      int rightHandY = (int)rightPoint.Y;
                      int leftHandX = (int)leftPoint.X;
                      int leftHandY = (int)leftPoint.Y;

                      DepthSpacePoint spineMidPoint = this.kinectSensor.CoordinateMapper.MapCameraPointToDepthSpace(spineMid.Position);
                      int spineMidX = (int)spineMidPoint.X;
                      int spineMidY = (int)spineMidPoint.Y;

                      DepthSpacePoint spineBasePoint = this.kinectSensor.CoordinateMapper.MapCameraPointToDepthSpace(spineBase.Position);
                      int spineBaseX = (int)spineBasePoint.X;
                      int spineBaseY = (int)spineBasePoint.Y;


                      int spineAverageX = (spineMidX + spineBaseX) / 2;
                      int spineAverageY = (spineMidY + spineBaseY) / 2;

                      //Find x and y from 1D array
                      int indexX = i % infraredWidth;
                      int indexY = i / infraredWidth;

                      //range to check
                      if (((indexX < rightHandX + 20 && indexX > rightHandX - 20) &&
                          (indexY < rightHandY + 20 && indexY > rightHandY - 20))
                          ||
                          ((indexX < leftHandX + 20 && indexX > leftHandX - 20) &&
                          (indexY < leftHandY + 20 && indexY > leftHandY - 20))) {
                          if (indexX < spineAverageX && indexY < spineAverageY) {
                              this.infraredPixels[colorPixelIndex++] = 0; //Blue
                              this.infraredPixels[colorPixelIndex++] = 0; //Green
                              this.infraredPixels[colorPixelIndex++] = intensity; //Red
                              this.infraredPixels[colorPixelIndex++] = 255;       //Alpha
                          }
                          else if (indexX > spineAverageX && indexY < spineAverageY) {
                              this.infraredPixels[colorPixelIndex++] = intensity; //Blue
                              this.infraredPixels[colorPixelIndex++] = 0; //Green
                              this.infraredPixels[colorPixelIndex++] = 0; //Red
                              this.infraredPixels[colorPixelIndex++] = 255;       //Alpha
                          }
                          else if (indexX < spineAverageX && indexY > spineAverageY) {
                              this.infraredPixels[colorPixelIndex++] = 0; //Blue
                              this.infraredPixels[colorPixelIndex++] = intensity; //Green
                              this.infraredPixels[colorPixelIndex++] = 0; //Red
                              this.infraredPixels[colorPixelIndex++] = 255;       //Alpha
                          }
                          else {
                              this.infraredPixels[colorPixelIndex++] = intensity; //Blue
                              this.infraredPixels[colorPixelIndex++] = intensity; //Green
                              this.infraredPixels[colorPixelIndex++] = 0; //Red
                              this.infraredPixels[colorPixelIndex++] = 255;       //Alpha
                          }

                          isRetroReflexiveBall = true;
                          break;
                      }


                  }

                  //The retroreflexive is not near a hand. Probably not something we have to track.
                  if (isRetroReflexiveBall == false) {
                      this.infraredPixels[colorPixelIndex++] = intensity; //Blue
                      this.infraredPixels[colorPixelIndex++] = intensity; //Green
                      this.infraredPixels[colorPixelIndex++] = intensity; //Red
                      this.infraredPixels[colorPixelIndex++] = 255;       //Alpha  
                  }

              }
              else {
                  this.infraredPixels[colorPixelIndex++] = intensity; //Blue
                  this.infraredPixels[colorPixelIndex++] = intensity; //Green
                  this.infraredPixels[colorPixelIndex++] = intensity; //Red
                  this.infraredPixels[colorPixelIndex++] = 255;       //Alpha  
              }
          }
      }

      private void RenderPixelArray(byte[] pixels) {
          Bitmap bmp;
          using (var ms = new MemoryStream(pixels)) {
              bmp = new Bitmap(ms);
          }
         // pixels.CopyTo(this.bitmap.PixelBuffer);
          Emgu.CV.Image<Bgr,Byte> image = new Emgu.CV.Image<Bgr, Byte>(bmp);
          this.circleImageBox.Image = image;
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
               CvInvoke.Circle(circleImage, System.Drawing.Point.Round(circle.Center), (int) circle.Radius, new Bgr(System.Drawing.Color.Brown).MCvScalar, 2);
               
            circleImageBox.Image = circleImage;
            #endregion

         }
      }

      private void textBox1_TextChanged(object sender, EventArgs e)
      {
         //PerformShapeDetection();
         
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
