# DorisRemoteFileUpload
Mockup for DORIS remote file upload


## Flow

### Get ro-crate manifest
Get the `ro-cate-manifest.json` in an existing dataset version.

**GET** `manifest/{datasetIdentifier}/{versionNumber}`

#### Responses:
* `200` if the dataset version exists, body contains the current ro-crate manifest 
* `404` if the dataset version does not exist

### Create dataset version (if it does not exist)
**POST** `manifest/{datasetIdentifier}/{versionNumber}`

#### Responses:
* `200` new dataset version created

### Upload file
Upload file to a dataset version.

**POST** `upload/{datasetIdentifier}/{versionNumber}`

#### Payload
* `file` form encoded file
* `folder` folder path
* `type` one of `data`, `documentation` or `metadata`

#### Responses:
* `200` file uploaded, return the updated manifest
* `405` if dataset version is published, return "not allowed"

