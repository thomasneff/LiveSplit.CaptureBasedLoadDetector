using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Accord.MachineLearning.VectorMachines.Learning;
using Accord.Statistics.Kernels;
using Accord.Math.Optimization.Losses;
using Accord.MachineLearning.VectorMachines;

namespace CaptureBasedLoadDetector
{

	
	public class DetectorParameters : ICloneable
	{
		#region Public Fields

		public int HistogramPatchSizeX = 50;
		public int HistogramPatchSizeY = 50;
		public int HistogramNumberOfBins = 16;
		public double MinLogLikelihoodPositive = 0;
		public double MaxLogLikelihoodNegative = double.MinValue;
		public double LogLikelihoodDistance = double.NegativeInfinity;
		public double LogLikelihoodThreshold = -10;
		public bool DetectsPerfectly = false;
		public SupportVectorMachine<Gaussian> SVM;

		public object Clone()
		{
			return this.MemberwiseClone();
		}


		#endregion Public Constructors
	}

	class Training
	{
		/// <summary>
		/// Loads all images from a chosen folder and computes all features for it.
		/// </summary>
		/// <param name="folder_path"></param>
		/// <returns></returns>
		public static double[][] FeaturesFromFolder(string folder_path, DetectorParameters detector_params)
		{


			DirectoryInfo d = new DirectoryInfo(folder_path);

			string[] extensions = new[] { ".jpg", ".bmp", ".png" };

			FileInfo[] files =
				d.GetFiles()
					 .Where(f => extensions.Contains(f.Extension.ToLower()))
					 .ToArray();

			List<double[]> features_all = new List<double[]>();

			foreach (var file in files)
			{
				Bitmap bmp = new Bitmap(file.FullName);

				//Make 32 bit ARGB bitmap
				Bitmap clone = new Bitmap(bmp.Width, bmp.Height,
					System.Drawing.Imaging.PixelFormat.Format32bppArgb);

				using (Graphics gr = Graphics.FromImage(clone))
				{
					gr.DrawImage(bmp, new Rectangle(0, 0, clone.Width, clone.Height));
				}

				List<double> features = FeatureDetector.featuresFromBitmapDouble(clone, detector_params);

				features_all.Add(features.ToArray());

				bmp.Dispose();
				clone.Dispose();
			}

			return features_all.ToArray();
		}

		public static DetectorParameters PerformTraining(double[][] positive_data, double[][] negative_data, DetectorParameters current_detector_params)
		{
			// Create a new One-class SVM learning algorithm
			var teacher = new OneclassSupportVectorLearning<Gaussian>()
			{
				UseKernelEstimation = true,
				Nu = 0.1
			};

			// Learn a support vector machine
			var svm = teacher.Learn(positive_data);
			current_detector_params.SVM = svm;

			int trues = 0;
			int falses = 0;

			foreach (double[] d_val in positive_data)
			{
				var prob = svm.Probability(d_val);
				var log_likelihood = svm.LogLikelihood(d_val);
				var decision = log_likelihood > current_detector_params.LogLikelihoodThreshold;

				current_detector_params.MinLogLikelihoodPositive = Math.Min(log_likelihood, current_detector_params.MinLogLikelihoodPositive);

				if (decision == true)
				{
					trues++;
				}
				else
				{
					falses++;
				}

			}

			System.Console.WriteLine("Positive Trues: " + trues.ToString() + " Falses: " + falses.ToString() + " (Min log-likelihood: " + current_detector_params.MinLogLikelihoodPositive.ToString() + ")");


			trues = 0;
			falses = 0;


			foreach (double[] d_val in negative_data)
			{
				var prob = svm.Probability(d_val);
				var log_likelihood = svm.LogLikelihood(d_val);
				var decision = log_likelihood > current_detector_params.LogLikelihoodThreshold;

				current_detector_params.MaxLogLikelihoodNegative = Math.Max(log_likelihood, current_detector_params.MaxLogLikelihoodNegative);

				if (decision == true)
				{
					trues++;
				}
				else
				{
					falses++;
				}

			}


			System.Console.WriteLine("Negative Trues: " + trues.ToString() + " Falses: " + falses.ToString() + " (Max log-likelihood: " + current_detector_params.MaxLogLikelihoodNegative.ToString() + ")");

			// We want the threshold to be more on the side of "not loading", as errors during gameplay are worse.
			current_detector_params.LogLikelihoodThreshold = (current_detector_params.MaxLogLikelihoodNegative + 5.0 * current_detector_params.MinLogLikelihoodPositive) / 6.0;
			current_detector_params.DetectsPerfectly = current_detector_params.MaxLogLikelihoodNegative < current_detector_params.MinLogLikelihoodPositive;
			current_detector_params.LogLikelihoodDistance = current_detector_params.MinLogLikelihoodPositive - current_detector_params.MaxLogLikelihoodNegative;

			return current_detector_params;
		}

		/// <summary>
		/// Opens 2 folders (loading / non-loading examples), and automatically trains and tunes the hyperparameters of the SVM detection.
		/// </summary>
		public static DetectorParameters OptimizeDetectorFromFolders()
		{

			// TODO: determine if accord+svm is fast enough for real-time detection
			// TODO: implement "record" tab in livesplit plugin to record data, which can then be used/sorted for training.


			// TODO: cleanup. load both datasets. fill them with more data (especially the regular gameplay case), then find best model with largest separation
			//			      we can easily do this be passing the number of bins, and patch sizes via grid search, compute features, etc.
			//				  the optimization criterion is largest difference between min and max log likelihood. we then simply choose the middle one.
			//				  these settings will then be stored in a JSON file so it's optimized per game.

			DetectorParameters best_detector_params = new DetectorParameters();
			DetectorParameters current_detector_params = new DetectorParameters();

			FolderBrowserDialog fbd = new FolderBrowserDialog();

			fbd.Description = "Choose a folder containing image patches captured during LOADING.";
			if (fbd.ShowDialog() != DialogResult.OK)
			{
				return null;
			}
			string positive_path = fbd.SelectedPath;

			fbd = new FolderBrowserDialog();

			fbd.Description = "Choose a folder containing image patches captured during regular gameplay.";
			if (fbd.ShowDialog() != DialogResult.OK)
			{
				return null;
			}
			string negative_path = fbd.SelectedPath;



			// Setup grid search parameters

			int[] patch_sizes_x = { 300 };//{ 10, 20, 25, 50, 100, 300 };
			int[] patch_sizes_y = { 10 };//{ 10, 20, 25, 50, 100 };
			int[] number_of_histogram_bins = { 8 };// { 2, 4, 8, 16, 32 };

			foreach (var patch_size_x in patch_sizes_x)
			{
				foreach (var patch_size_y in patch_sizes_y)
				{
					foreach (var histogram_bins in number_of_histogram_bins)
					{
						System.Console.WriteLine("Training: pX = " + patch_size_x + ", pY = " + patch_size_y + ", bins = " + histogram_bins);

						current_detector_params = new DetectorParameters();
						current_detector_params.HistogramPatchSizeX = patch_size_x;
						current_detector_params.HistogramPatchSizeY = patch_size_y;
						current_detector_params.HistogramNumberOfBins = histogram_bins;

						var positive_data = FeaturesFromFolder(positive_path, current_detector_params);
						var negative_data = FeaturesFromFolder(negative_path, current_detector_params);

						current_detector_params = PerformTraining(positive_data, negative_data, current_detector_params);

						if(current_detector_params.DetectsPerfectly && current_detector_params.LogLikelihoodDistance > best_detector_params.LogLikelihoodDistance)
						{
							best_detector_params = (DetectorParameters) current_detector_params.Clone();
						}

					}
				}
			}



			System.Console.WriteLine("Best parameters: ");
			System.Console.WriteLine("HistogramPatchSizeX: " + best_detector_params.HistogramPatchSizeX);
			System.Console.WriteLine("HistogramPatchSizeY: " + best_detector_params.HistogramPatchSizeY);
			System.Console.WriteLine("HistogramNumberOfBins: " + best_detector_params.HistogramNumberOfBins);
			System.Console.WriteLine("MinLogLikelihoodPositive: " + best_detector_params.MinLogLikelihoodPositive);
			System.Console.WriteLine("MaxLogLikelihoodNegative: " + best_detector_params.MaxLogLikelihoodNegative);
			System.Console.WriteLine("LogLikelihoodDistance: " + best_detector_params.LogLikelihoodDistance);
			System.Console.WriteLine("LogLikelihoodThreshold: " + best_detector_params.LogLikelihoodThreshold);

			return best_detector_params;
		}


	}
}
