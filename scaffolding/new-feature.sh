#!/bin/bash

################################################################################
# Scaffolding helper for creating a new CM Communityization project and its first
# feature in this repository.
#
# What this script does:
# - Verifies the required CM npm packages are installed and up to date.
# - Resolves the MES-specific `@criticalmanufacturing/ngx-schematics` version.
# - Creates a project under `features/<ProjectName>`.
# - Initializes the project root with `cmf init`.
# - Asks whether to organize the Communityization as a Feature (opt-in) or as a
#   root package with layer sub-packages (default).
# - Prompts for the layers to generate:
#   Business, Data, HTML, Help, IoT, Database, Tests, Reporting, Grafana,
#   and Security Portal.
#
# Naming model:
# - Root package: `Cmf.Community`
# - Default (normal) path: layers are sub-packages of the root package, e.g.
#   `Cmf.Community.Business`, `Cmf.Community.HTML`, generated directly under the
#   project directory.
# - Opt-in Features path: creates a `Community.<Feature>` feature under
#   `Features/` and generates layers inside it, e.g.
#   `Cmf.Community.Community.<Feature>.Business`. Only used if the user
#   explicitly asks for this structure.
#
# Current CLI workaround (Features path only):
# - With tenant fixed to `Community` and feature names also prefixed with
#   `Community.`, the current CM CLI can generate duplicated namespace/project
#   segments such as `Community.Community.<Feature>`.
# - `fix_duplication` normalizes those generated files after layer creation so
#   packageIds, namespaces, csproj names, and solution references match the
#   intended feature naming. This is not needed in the default root-package
#   path since there is no feature/tenant segment to collide.
################################################################################

# Exit immediately if a command exits with a non-zero status.
set -e

# Resolve paths. Base directory = parent of the scaffolding script dir (repo root).
scriptPath="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
baseDir=$(dirname "$scriptPath")
featuresBase="$baseDir/features"
deploymentBaseDir="./deploymentdir"

# --- helpers ---------------------------------------------------------------

prompt_required() {
    local msg="$1"
    local value=""
    while [ -z "$value" ]; do
        read -r -p "$msg " value
        if [ -z "$value" ]; then
            echo "  (value cannot be empty)" >&2
        fi
    done
    printf '%s' "$value"
}

mesVersion=$(prompt_required "MES Version (e.g. 11.1.7):")

echo "Starting scaffolding for MES Version: $mesVersion"
mesDistTag="release-$mesVersion"
mesDistTag=${mesDistTag//./}
echo "Using MES dist-tag: $mesDistTag"

prompt_yn() {
    local msg="$1"
    local ans
    read -r -p "$msg [y/N] " ans
    [[ "$ans" =~ ^[Yy]([Ee][Ss])?$ ]]
}

validate_name() {
    local label="$1"
    local value="$2"
    if [[ "$value" =~ [\ \-\.] ]]; then
        echo "Error: $label must not contain spaces, hyphens, or dots." >&2
        exit 1
    fi
}

fix_duplication() {
    # Workaround for cmf-cli LayerTemplateCommand: it builds --idSegment as
    # "{Tenant}.{featureName}". With Tenant forced to "Community" via `cmf init
    # --tenant Community` and the feature packageId of the form "Community.<X>",
    # this produces "Community.Community.<X>" in namespaces, csproj filenames,
    # and .sln references. Collapse the duplicated "Community" segment so
    # packageIds stay correct while namespaces / csproj names end up as
    # "Community.<X>".
    local target_dir="$1"
    local suffix="$2"
    local dup="Community.Community.${suffix}"
    local single="Community.${suffix}"

    [ -d "$target_dir" ] || return 0

    echo "Fixing namespace duplication under $target_dir..."
    echo "  searching for: \"${dup}\""
    echo "  replacing with: \"${single}\""

    local escaped_dup
    escaped_dup=$(printf '%s' "$dup" | sed 's|[][\.*^$/]|\\&|g')

    local content_count=0
    local rename_count=0
    local f newf

    while IFS= read -r f; do
        sed -i "s|${escaped_dup}|${single}|g" "$f"
        echo "  modified: ${f#$target_dir/}"
        content_count=$((content_count + 1))
    done < <(find "$target_dir" -type f \
        \( -name "*.cs" -o -name "*.csproj" -o -name "*.sln" -o -name "*.props" \
         -o -name "*.targets" -o -name "*.json" -o -name "*.xml" -o -name "*.config" \
         -o -name "*.cshtml" -o -name "*.razor" -o -name "*.ts" -o -name "*.tsx" \
         -o -name "*.html" -o -name "*.md" -o -name "*.yml" -o -name "*.yaml" \
         -o -name "*.ps1" -o -name "*.sh" \) \
        -not -path "*/node_modules/*" -not -path "*/bin/*" -not -path "*/obj/*" \
        -not -path "*/dist/*" \
        -exec grep -l -F "$dup" {} +)

    while IFS= read -r f; do
        newf="${f//${dup}/${single}}"
        if [ "$f" != "$newf" ]; then
            mv -- "$f" "$newf"
            echo "  renamed:  ${f#$target_dir/} -> ${newf#$target_dir/}"
            rename_count=$((rename_count + 1))
        fi
    done < <(find "$target_dir" -depth -name "*${dup}*" -not -path "*/node_modules/*")

    echo "  Summary: $content_count file(s) content-updated, $rename_count path(s) renamed."
}

check_cm_package() {
    local pkg="$1"
    echo "Checking $pkg..."

    local latest
    latest=$(npm view "$pkg" version 2>/dev/null)
    if [ -z "$latest" ]; then
        echo "  Error: could not fetch latest version of $pkg from npm registry."
        exit 1
    fi

    
    local next
    next=$(npm view "$pkg" dist-tags.next 2>/dev/null)

    local installed
    installed=$(npm ls -g --depth=0 "$pkg" 2>/dev/null | grep -oE "${pkg}@[0-9][^ ]*" | head -1 | sed "s|^${pkg}@||")

    if [ -z "$installed" ]; then
        echo "  Error: $pkg is not installed globally (latest is $latest and next is ${next:-<none>})."
        echo "  Install with: npm install -g ${pkg}@latest" or "npm install -g ${pkg}@next"
        exit 1
    fi

    if [ "$installed" != "$latest" ] && [ "$installed" != "$next" ]; then
        echo "  Error: $pkg installed version ($installed) does not match latest ($latest) or next ($next)."
        echo "  Update with: npm install -g ${pkg}@latest" or "npm install -g ${pkg}@next"
        exit 1
    fi

    echo "  OK: $pkg@$installed is up to date."
}

# --- preflight -------------------------------------------------------------

check_cm_package "@criticalmanufacturing/cli"
check_cm_package "@criticalmanufacturing/portal"

ngxSchematicsVersion=$(npm view @criticalmanufacturing/ngx-schematics@"$mesDistTag" version)
echo "Using CM ngx-schematics version: $ngxSchematicsVersion"

# --- prompt for project ----------------------------------------------------

projectName=$(prompt_required "Project name:")
validate_name "Project name" "$projectName"

projectDir="$featuresBase/$projectName"
echo "Project directory will be: $projectDir"

mkdir -p "$projectDir"
cd "$projectDir"
mkdir -p "$deploymentBaseDir"

###################
echo "1. Init project at $projectDir"
###################

if [ -f "$projectDir/cmfpackage.json" ]; then
    echo "  Skipping: $projectDir/cmfpackage.json already exists."
else
    cmf -l Debug init "$projectName" "Cmf.Community.$projectName" \
        --version 1.0.0 \
        --tenant "Community" \
        --infra "$scriptPath/infra.json" \
        --config "$scriptPath/env.json" \
        --MESVersion "$mesVersion" \
        --ngxSchematicsVersion "$ngxSchematicsVersion" \
        --nugetVersion "$mesVersion" \
        --testScenariosNugetVersion "$mesVersion" \
        --deploymentDir "$deploymentBaseDir"
fi

# --- optional Features structure -------------------------------------------

###################
echo "2. Choose project structure"
###################

useFeatureStructure=false
if prompt_yn "Organize this as a Feature under Features/<Project>/Community.<Feature>? (advanced; default is a root package with layer sub-packages)"; then
    useFeatureStructure=true
fi

if [ "$useFeatureStructure" = true ]; then
    featureSuffix=$(prompt_required "Feature name (will be prefixed with 'Community.'):")
    validate_name "Feature name" "$featureSuffix"

    featureFullName="Community.$featureSuffix"
    featurePath="$projectDir/Features/$featureFullName"

    echo "Create feature $featureFullName"

    if [ -d "$featurePath" ]; then
        echo "  Skipping: $featurePath already exists."
    else
        cmf -l Debug new feature "$featureFullName"
    fi

    cd "$featurePath"
    packagePrefix="Cmf.Community.$featureFullName"
else
    packagePrefix="Cmf.Community"
fi

# --- interactive layer creation -------------------------------------------

echo ""
echo "Select which Communityization layers to create for $packagePrefix:"

if prompt_yn "Add Business layer?"; then
    cmf -l Debug new business
fi

if prompt_yn "Add Data layer?"; then
    cmf -l Debug new data --businessPackage "./$packagePrefix.Business/"
fi

if prompt_yn "Add HTML layer?"; then
    cmf -l Debug new html
fi

if prompt_yn "Add Help layer?"; then
    cmf -l Debug new help
fi

if prompt_yn "Add IoT layer?"; then
    cmf -l Debug new iot --htmlPackageLocation "./$packagePrefix.HTML/"
fi

if prompt_yn "Add Database layer?"; then
    cmf -l Debug new database
fi

if prompt_yn "Add Tests layer?"; then
    cmf -l Debug new test
fi

if prompt_yn "Add Reporting layer?"; then
    cmf -l Debug new reporting
fi

if prompt_yn "Add Grafana layer?"; then
    cmf -l Debug new grafana
fi

if prompt_yn "Add Security Portal layer?"; then
    cmf -l Debug new securityPortal
fi

if [ "$useFeatureStructure" = true ]; then
    fix_duplication "$featurePath" "$featureSuffix"
    echo ""
    echo "Done. Feature ready at: $featurePath"
else
    echo ""
    echo "Done. Package layers ready at: $projectDir"
fi
