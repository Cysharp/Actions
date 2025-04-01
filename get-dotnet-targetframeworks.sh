#!/bin/bash
set -euo pipefail

# Script to find all .NET Target Frameworks used in a repository and output for GitHub Actions

# Get the repository root directory (assuming script is run from the repo root)
repo_root=$(pwd)

# Create a temporary file to store frameworks
temp_file=$(mktemp)

echo "::group::Scanning for target frameworks..."

# Find all project files and Directory.Build.props files
find "$repo_root" -type f \( -name "*.csproj" -o -name "*.fsproj" -o -name "*.vbproj" -o -name "Directory.Build.props" \) -print0 |
while IFS= read -r -d '' file; do
    echo "Checking file: $file"

    # Extract single target framework
    grep -oP '<TargetFramework>\K[^<]+' "$file" >> "$temp_file" || true

    # Extract multiple target frameworks
    if grep -q '<TargetFrameworks>' "$file"; then
        frameworks=$(grep -oP '<TargetFrameworks>\K[^<]+' "$file" || true)
        if [ -n "$frameworks" ]; then
            echo "$frameworks" | tr ';' '\n' >> "$temp_file"
        fi
    fi

    # For Directory.Build.props files, also check for other framework setting patterns
    if [[ "$file" == *"Directory.Build.props"* ]]; then
        echo "  Found Directory.Build.props: $file"
        # Check for PropertyGroup with Condition containing TargetFramework
        prop_groups=$(grep -oP '<PropertyGroup .*?TargetFramework.*?>\K.*?</PropertyGroup>' "$file" || true)
        if [ -n "$prop_groups" ]; then
            echo "$prop_groups" | grep -oP "=='([^']+)'" | cut -d "'" -f 2 >> "$temp_file" || true
        fi
    fi
done
echo "::endgroup::"

# Extract SDK versions from framework strings
echo "::group::Parsing .NET SDK versions..."
sdk_versions_file=$(mktemp)

# Process frameworks to get SDK versions
sort "$temp_file" | uniq | grep -v "^$" | while read -r framework; do
    if [[ -n "$framework" ]]; then
        echo "Processing framework: $framework"

        # Extract version from net* frameworks
        if [[ $framework == net* ]]; then
            if [[ $framework =~ ^net([0-9]+)\.([0-9]+)$ ]]; then
                # For formats like net6.0
                echo "${BASH_REMATCH[1]}.${BASH_REMATCH[2]}" >> "$sdk_versions_file"
            elif [[ $framework =~ ^net([0-9]+)$ ]]; then
                # For formats like net5, net6
                echo "${BASH_REMATCH[1]}.0" >> "$sdk_versions_file"
            elif [[ $framework == netcoreapp* ]]; then
                # For netcoreapp formats like netcoreapp3.1
                version=$(echo "$framework" | sed 's/netcoreapp//')
                echo "$version" >> "$sdk_versions_file"
            fi
        fi
    fi
done
echo "::endgroup::"

# Sort and deduplicate SDK versions
sorted_versions=$(sort -V "$sdk_versions_file" | uniq | grep -v "^$" | tr '\n' ' ')

# Generate GitHub Actions output using the new environment file approach
if [ -n "${GITHUB_OUTPUT:-}" ]; then
    echo "dotnet-versions=$sorted_versions" | tee -a "$GITHUB_OUTPUT"
    echo "dotnet-json=$(sort -V "$sdk_versions_file" | uniq | grep -v "^$" | jq -R . | jq -s . | tr -d '\n')" | tee -a "$GITHUB_OUTPUT"
else
    echo "GITHUB_OUTPUT environment variable not set. This script is designed to run in GitHub Actions."
    echo "If running locally, here's what would be set:"
    echo "dotnet-versions=$sorted_versions"
    echo "dotnet-json=$(sort -V "$sdk_versions_file" | uniq | grep -v "^$" | jq -R . | jq -s . | tr -d '\n')"
fi

# Also print human-readable output
echo "::group::Results for GitHub Actions"
echo "Detected .NET SDK versions for actions/setup-dotnet:"
echo "$sorted_versions"
echo
echo "JSON array format:"
sort -V "$sdk_versions_file" | uniq | grep -v "^$" | jq -R . | jq -s . || echo "jq not installed, cannot format JSON"
echo "::endgroup::"

# Clean up
rm -f "$temp_file" "$sdk_versions_file"
