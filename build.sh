# To define the environment variable, put something like this in your .bashrc file:
# export VINTAGE_STORY_DEV="$HOME/software/vintagestory_dev"

RED='\033[0;31m'
NC='\033[0m' # No Color

null_textured_shapes=$(grep -rl "#null" assets/)
# Only print anything if files were found
if [[ -n $null_textured_shapes ]]; then
    echo -e "${RED}These shape files contain null textures:"
    echo -e "${null_textured_shapes}${NC}"
fi

dotnet run --project ./Build/CakeBuild/CakeBuild.csproj -- "$@"
rm -r bin/
rm -r src/obj/
rm "${VINTAGE_STORY_DEV}"/Mods/SaltAndSands-*.zip
cp Build/Releases/SaltAndSands-*.zip "${VINTAGE_STORY_DEV}/Mods"
