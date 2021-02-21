# Start Azure Container Instance from Azure Function

Requires the Azure CLI and Azure Function CLI

Log in in Azure cli

`az login`

Create Auth file,

`az ad sp create-for-rbac --sdk-auth > my.azureauth`

Copy auth file to project folder, and set 'Copy to output directory' to copy the fie.

Run the function

`func start`

Trigger the function

`curl -X POST -d '' http://localhost:7071/api/TriggerContainer`

## To Do

- Re-run &amp; trigger the function
- Once container is running... 
- Connect to the container `bin/sh`
- Check files are in the correct place
- Then execute `openscad -o /src/outputfile.stl /SCAD/minkowski_Board.scad`
