$path = 'c:\Users\yjz\tsinghua-ProjectA\tsinghua-ProjectA\Assets\Scripts\Meau2Controller.cs.meta'
$guid = [guid]::NewGuid().ToString()
$content = @"
fileFormatVersion: 2
guid: $guid
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
$bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
[System.IO.File]::WriteAllBytes($path, $bytes)
