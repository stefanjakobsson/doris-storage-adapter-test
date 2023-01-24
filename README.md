# Doris Remote File Upload
Mockup for DORIS remote file upload


## Flow

### Get auth status
Get the status of existing session.
The remote endpoint needs to have the header `Access-Control-Allow-Origin: https://staging.snd.gu.se`


1. Do GET (fetch) `https://upload.example.org/sessionStatus`

2. If session exist return `200` 

3. If `401` set header 
`Redirect: https://example.org/login?redirect=https://upload.example.org/sessionStatus`
 


### Get ro-crate manifest
Get the `ro-cate-manifest.json` in an existing dataset version.

**GET** `{datasetIdentifier}/{versionNumber}/manifest`

#### Responses:
* `200` if the dataset version exists, body contains the current ro-crate manifest 
* `404` if the dataset version does not exist

### Create new dataset version / update manifest
Create or updates a manifest for a dataset version.
If post fails, DORIS will show an error message to the user and retry every x-minute.

Manifest will contain:
* conditionsOfAccess PUBLIC / other `http://publications.europa.eu/resource/authority/access-right`
* Person objects with eduPersonPrincipalName
* publicationDate set if dataset version is published


**POST** `{datasetIdentifier}/{versionNumber}/manifest`

#### Responses:
* `200` manifest created

### Create dataset version (if it does not exist)
**POST** `{datasetIdentifier}/{versionNumber}/manifest`

#### Responses:
* `200` new dataset version created

### Upload file
Upload or update a file for a dataset version.

**POST** `{datasetIdentifier}/{versionNumber}`

#### Payload
* `file` form encoded file
* `folder` folder path
* `type` one of `data`, `documentation` or `metadata`

#### Result example
```json
{
  "@type": "File",
  "@id": "data/data.csv",
  "contentSize": 4242,
  "dateCreated": "2022-02-21T11:45:20Z",
  "dateModified": "2022-02-22T15:50:30Z",
  "encodingFormat": "text/csv",
  "url": "https://example.org/record/04679b46-964c-11ec-b909-0242ac120002/data.csv"
}
```

#### Responses:
* `200` file uploaded, return the updated manifest
* `405` if dataset version is published, return "not allowed"

### Delete file
Delete file to a dataset version.

**DELETE** `{datasetIdentifier}/{versionNumber}`

#### Payload
* `filepath` path to the file


# Dicussion points
* Session for user swamid or simple flow with token/otp for file upload?
* Should files be uploaded and changes via DORIS only or should other methods be allowed (SAMBA, SFTP etc.)
* Add `Access-Control-Allow-Origin: https://staging.snd.gu.se` to all controllers