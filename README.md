# streaming_azure_media_encoder

## A project to run multiple ffmpeg encoding agents in a vm scaleset in Azure.

### Problem

You like to encode a media file into several different target formats using a cloud service to leverage the power of the cloud for faster encoding. The challenge herewith often lies in starting the encoding as soon as the upload of your media asset to the cloud starts. If you would use Azure Media Services for this task, you could probably get the same results but the way how Azure Media Services handles encodings is: You upload your file to a blob storage completely, then you start your rendition tasks in Azure Media Service, which then copies the results to another blob storage. If time is key and you like to start encoding already while still uploading a custom solution is needed, which this project delivers.

### Solution

Using the Azure Resource Manager and an ARM-template, the deployment scripts in this solution create a rendering environment that has one jumphost to access your rendering agent machines and a vm scaleset of machines that each host the rendering agent.
The rendering agents are set up by the deployment scripts to constantly check a storage queue for rendering jobs. As they compete for the jobs in the queue, each rendering agent takes a job that is available from the queue and removes it from the queue afterwards. So you can pump several jobs into the queue and all the agents will take jobs from the queue as long as there is jobs available.
After taking a job from the queue, a rendering agent starts ffmpeg and ecodes your media file to the local disk. Once done with encoding it uploads the file to your target storage account, which you can configure in the deployment script.
The key solution is that the Uploader tool chunks your media file into 4MB chunks and uploads them into the source storage folder. The agents check this folder for new chunks frequently and as soon as they find a new chunk that they are waiting for, they feed this chunk into ffmpeg.
