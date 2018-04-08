# LiveSplit.CaptureBasedLoadDetector
LiveSplit component to automatically detect and remove loads for games using image capture.

This is adapted from my standalone detection tool https://github.com/thomasneff/CrashNSaneTrilogyLoadDetector
and from https://github.com/Maschell/LiveSplit.PokemonRedBlue for the base component code.

# Credits / Acknowledgment
This Component uses the Accord.NET ( http://accord-framework.net/index.html ) machine learning framework for OneClass SVM detection (source at https://github.com/accord-net/framework ).
This Component uses Newtonsoft JSON.NET ( http://www.newtonsoft.com/json ) for serializing options (source at https://github.com/JamesNK/Newtonsoft.Json ). 

# Special Thanks
Special thanks go to McCrodi from the Crash Speedrunning Discord, who helped me by providing 1080p/720p captured data and general feedback regarding the functionality.

# How does it work?
The method works by taking a small "screenshot" (currently 300x100) from your selected capture at the center, where "LOADING" is displayed when playing. It then cuts this 300x100 image into patches. From these patches, a color histogram is computed of the red, green and blue color channels. These histograms are put into a large vector, which describes our image (feature vector).

We use these features to train a OneClass SVM machine learning model, powered by the Accord.NET ( http://accord-framework.net/index.html ) machine learning framework (source at https://github.com/accord-net/framework ).
For training, it is necessary to capture images of regular gameplay as well as load screens. For this, the tool provides a 'capture' tab, that allows you to specify the location of capture. When enabled, images of size 300x100 are captured every frame.

Then it is necessary to create 2 folders, one containing only images captured during gameplay (non-loading) and one containing only loading images. The loading images are more important, so make sure you capture a sufficient amount when re-training the SVM!

To detect if a screen is "LOADING" or not, we compute our feature vector every ~4-16ms (depending on capture modes, fast enough for real-time load detection) and classify it using the SVM.

After training, all settings as well as the trained SVM model are stored in a separate folder called "CaptureBasedLoadDetector", and can be shared for different games/communities.



# Settings
The LiveSplit.CaptureBasedLoadDetector.dll goes into your "Components" folder in your LiveSplit folder.

Add this to LiveSplit by going into your Layout Editor -> Add -> Control -> CaptureBasedLoadDetector.

You can specify to capture either the full primary Display (default) or an open window. This window has to be open (not minimized) but does not have to be in the foreground.

This might not work for windows with DirectX/OpenGL surfaces, nothing I can do about that. (Use Display capture for those cases, sorry). In those cases, you will probably get a black image in the capture preview in the component settings.
