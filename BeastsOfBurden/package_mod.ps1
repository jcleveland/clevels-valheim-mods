# Mod DLL
$mod_name = "BeastsOfBurden"
$project_dir = ".\"
$output_dir = $project_dir + "\bin\Release\"
$mod_dll = $output_dir + $mod_name + ".dll"

# Get mod version from assembly version info
$mod_version = (Get-Command $mod_dll).FileVersionInfo.FileVersion

# Locations to put artifacts
$artifact_path = $output_dir + "artifacts\"
$nexus_path = $artifact_path + "nexus\"
$tsio_path = $artifact_path + "ts_io\"
$raw_path = $artifact_path + "raw\" + $mod_name + $mod_version
New-Item -ItemType Directory -Force -Path $artifact_path
New-Item -ItemType Directory -Force -Path $nexus_path
New-Item -ItemType Directory -Force -Path $tsio_path
New-Item -ItemType Directory -Force -Path $raw_path


###################################
####### Raw DLL
###################################
Copy-Item $mod_dll $raw_path

###################################
####### Nexus Packaging
###################################
$nexus_zip = $nexus_path + $mod_name + "_v" + $mod_version + ".zip"
Compress-Archive -Path $mod_dll -DestinationPath $nexus_zip -Force

###################################
####### Thunderstore packaging
###################################
# Create temp directory for TSIO package
$tsio_tmp_directory = $output_dir + "ts_io_tmp\" + $mod_name + $mod_version + "\"
New-Item -ItemType Directory -Force -Path $tsio_tmp_directory

# Update Manifest to have correct version number
$manifest = Get-Content $project_dir"resources\manifest.json" -raw | ConvertFrom-Json
$manifest.version_number = $mod_version
$manifest | ConvertTo-Json -depth 32| set-content $tsio_tmp_directory"manifest.json"

# Copy README and icon into tmp directory
Copy-Item $project_dir"README.md" $tsio_tmp_directory
Copy-Item $project_dir"resources\icon.png" $tsio_tmp_directory

# Copy mod dll into tmp file\plugins directory
New-Item -ItemType Directory -Path $tsio_tmp_directory"files\plugins\" -Force
Copy-Item $mod_dll $tsio_tmp_directory"files\plugins\" -Force

$tsio_zip = $tsio_path + $mod_name + "_v" + $mod_version + ".zip"
#Archive and then cleanup tsio directory
Compress-Archive -Path $tsio_tmp_directory\* -DestinationPath $tsio_zip -Force
Remove-Item  $tsio_tmp_directory -Recurse
