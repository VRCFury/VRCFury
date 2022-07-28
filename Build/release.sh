#!/bin/sh
set -e

# Based on https://github.com/quabug/unity-pack

output_path=$1
path_prefix=$2
root_guid=$3
tmp_dir=`mktemp -d -t unitypackage-XXXXXXXX`

function make_meta_directory() {
    meta_file=$(echo "$1" | cut -d/ -f2-)
    asset_file=${meta_file%.*}
    
    if [[ $(echo "$meta_file" | cut -d/ -f1) == "Build" ]]; then
      return
    fi
    
    if [[ ! -e "$asset_file" ]]; then
      echo "Cannot find corresponding asset file $asset_file" >&2
      return
    fi

    echo "Adding $asset_file to $path_prefix/$asset_file"
    guid=$(yq e '.guid' "$meta_file")
    dir="$tmp_dir/$guid"
    mkdir $dir
    cp $meta_file $dir/asset.meta
    echo $path_prefix/$asset_file > $dir/pathname
    if [[ -f "$asset_file" ]]; then
      cp $asset_file $dir/asset
    fi
}

find . -name "*.meta" -print0 \
  | while IFS= read -r -d '' file; do make_meta_directory "$file"; done

mkdir "$tmp_dir/$root_guid"
echo -n "$path_prefix" > "$tmp_dir/$root_guid/pathname"
cat <<EOT > "$tmp_dir/$root_guid/asset.meta"
fileFormatVersion: 2
guid: $root_guid
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOT

cd $tmp_dir
tar -czvf archtemp.tar.gz * > /dev/null
cd - > /dev/null
mv $tmp_dir/archtemp.tar.gz $output_path
echo $output_path
