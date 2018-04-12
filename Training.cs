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
using AForge.Imaging.Filters;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

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
		public static int NumberOfAugmentedImages = 20;

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

			bool debug_enabled = true;

			if (debug_enabled)
				Directory.CreateDirectory("aug_imgs");

			int img_it = 0;
			foreach (var file in files)
			{
				Bitmap bmp = new Bitmap(file.FullName);
				img_it++;
				for (int i = 0; i < NumberOfAugmentedImages; ++i)
				{
					//Make 32 bit ARGB bitmap
					Bitmap clone = new Bitmap(bmp.Width, bmp.Height,
						System.Drawing.Imaging.PixelFormat.Format32bppArgb);

					Random rng = new Random();

					int x_off_random = rng.Next((-clone.Width / 20), (clone.Width / 20) + 1);
					int y_off_random = rng.Next((-clone.Height / 20), (clone.Height / 20) + 1);

					int x_size_random = rng.Next((-clone.Width / 10), (clone.Width / 10) + 1);
					int y_size_random = rng.Next((-clone.Height / 10), (clone.Height / 10) + 1);

					int do_blur = rng.Next(0, 2);

					using (Graphics gr = Graphics.FromImage(clone))
					{ 
						gr.DrawImage(bmp, new Rectangle(0, 0, clone.Width, clone.Height));
						gr.DrawImage(bmp, new Rectangle(x_off_random, y_off_random, clone.Width + x_size_random, clone.Height + y_size_random));
					}


					/*if(resize_factor == 2)
					{
						ResizeBilinear filter = new ResizeBilinear(clone.Width / 2, clone.Height / 2);
						var resized = filter.Apply(clone);

						filter = new ResizeBilinear(clone.Width, clone.Height);
						clone = filter.Apply(resized);
					}

					if (resize_factor == 3)
					{
						ResizeNearestNeighbor filter = new ResizeNearestNeighbor(clone.Width / 2, clone.Height / 2);
						var resized = filter.Apply(clone);

						filter = new ResizeNearestNeighbor(clone.Width, clone.Height);
						clone = filter.Apply(resized);
					}

					if (resize_factor == 4)
					{
						ResizeBilinear filter = new ResizeBilinear(clone.Width / 4, clone.Height / 4);
						var resized = filter.Apply(clone);

						filter = new ResizeBilinear(clone.Width, clone.Height);
						clone = filter.Apply(resized);
					}
					*/

					int resamplingFactor = Math.Min((1 + i/2), 10);

					ResizeBilinear filter = new ResizeBilinear(clone.Width / resamplingFactor, clone.Height / resamplingFactor);
					var resized = filter.Apply(clone);

					filter = new ResizeBilinear(clone.Width, clone.Height);
					clone = filter.Apply(resized);

		

					List<double> features = FeatureDetector.featuresFromBitmapDouble(clone, detector_params, 5);

					features_all.Add(features.ToArray());
					if (img_it < 2 && debug_enabled)
						clone.Save("aug_imgs/aug_" + img_it + "_" + i + ".jpg", ImageFormat.Jpeg);
					clone.Dispose();
				}

				bmp.Dispose();

			}

			return features_all.ToArray();
		}

		public static DetectorParameters PerformTrainingBinarySVM(double[][] positive_data, double[][] negative_data, DetectorParameters current_detector_params)
		{
			var teacher = new SequentialMinimalOptimization<Gaussian>()
			{
				UseKernelEstimation = true,
				UseComplexityHeuristic = true
			};

			var data = new double[positive_data.GetLength(0) + negative_data.GetLength(0)][];

			List<double[]> data_examples = new List<double[]>();
			List<double> targets = new List<double>();


			for (int i = 0; i < positive_data.GetLength(0); ++i)
			{
				data_examples.Add(positive_data[i]);
				targets.Add(0);
			}

			for (int i = 0; i < negative_data.GetLength(0); ++i)
			{
				data_examples.Add(negative_data[i]);
				targets.Add(1);
			}


			var all_examples = data_examples.ToArray();
			var all_targets = targets.ToArray();



			// Learn a support vector machine
			var svm = teacher.Learn(all_examples, all_targets);
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
			current_detector_params.LogLikelihoodThreshold = (3.5 * current_detector_params.MaxLogLikelihoodNegative + 2.5 * current_detector_params.MinLogLikelihoodPositive) / 6.0;
			current_detector_params.DetectsPerfectly = current_detector_params.MaxLogLikelihoodNegative < current_detector_params.MinLogLikelihoodPositive;
			current_detector_params.LogLikelihoodDistance = current_detector_params.MinLogLikelihoodPositive - current_detector_params.MaxLogLikelihoodNegative;

			return current_detector_params;
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
			current_detector_params.LogLikelihoodThreshold = (3.5 * current_detector_params.MaxLogLikelihoodNegative + 2.5 * current_detector_params.MinLogLikelihoodPositive) / 6.0;
			current_detector_params.DetectsPerfectly = current_detector_params.MaxLogLikelihoodNegative < current_detector_params.MinLogLikelihoodPositive;
			current_detector_params.LogLikelihoodDistance = current_detector_params.MinLogLikelihoodPositive - current_detector_params.MaxLogLikelihoodNegative;

			return current_detector_params;
		}

		/// <summary>
		/// Opens 2 folders (loading / non-loading examples), and automatically trains and tunes the hyperparameters of the SVM detection.
		/// </summary>
		public static DetectorParameters OptimizeDetectorFromFolders(ImageCaptureInfo info, string settingsPath)
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
			fbd.RootFolder = System.Environment.SpecialFolder.MyComputer;
			fbd.SelectedPath = settingsPath;

			fbd.Description = "Choose a folder containing image patches captured during LOADING.";
			if (fbd.ShowDialog() != DialogResult.OK)
			{
				return null;
			}
			string positive_path = fbd.SelectedPath;

			fbd = new FolderBrowserDialog();
			fbd.RootFolder = System.Environment.SpecialFolder.MyComputer;
			fbd.SelectedPath = settingsPath;

			fbd.Description = "Choose a folder containing image patches captured during regular gameplay.";
			if (fbd.ShowDialog() != DialogResult.OK)
			{
				return null;
			}
			string negative_path = fbd.SelectedPath;



			// Setup grid search parameters

			int[] patch_sizes_x = { info.featureSizeX, info.featureSizeX / 2 , info.featureSizeX  / 4, info.featureSizeX  / 8};//{ 10, 20, 25, 50, 100, 300 };
			int[] patch_sizes_y = { info.featureSizeY, info.featureSizeY / 2, info.featureSizeY / 4, info.featureSizeY / 8 };//{ 10, 20, 25, 50, 100 };
			int[] number_of_histogram_bins = {4, 8, 16};

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
