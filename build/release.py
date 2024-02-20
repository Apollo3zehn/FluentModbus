import subprocess
import sys
import re
import json

import requests

import version

# get release version
final_version = f"v{version.get_version(build=None)}"
print(f"release version: {final_version}")

# check if on master branch
if not "* master" in subprocess.check_output(["git", "branch"], stdin=None, stderr=None, shell=False).decode("utf8"):
    raise Exception("Must be on master branch.")

print("  master branch: OK")

# check if release version already exist
with open("solution.json", "r") as fh:
    solution_data = json.load(fh)

pattern = r"https:\/\/github.com\/(.*)"
request_url = re.sub(pattern, r"https://api.github.com/repos/\1/releases", solution_data["repository-url"])

headers = {
    "Authorization": f"token {sys.argv[1]}",
    "User-Agent": "Nexus",
    "Accept": "application/vnd.github.v3+json"
}

response = requests.get(request_url, headers=headers)
response.raise_for_status()
releases = response.json()

if final_version in (release["name"] for release in releases):
    raise Exception(f"Release {final_version} already exists.")

print("    unique release: OK")

# get annotation
with open("CHANGELOG.md") as file:
    changelog = file.read()
    
matches = list(re.finditer(r"^##\s(.*?)\s-\s[0-9]{4}-[0-9]{2}-[0-9]{2}(.*?)(?=(?:\Z|^##\s))", changelog, re.MULTILINE | re.DOTALL))

if not matches:
    raise Exception(f"The file CHANGELOG.md is malformed.")

match_for_version = next((match for match in matches if match[1] == final_version), None)

if not match_for_version:
    raise Exception(f"No change log entry found for version {final_version}.")

release_message = match_for_version[2].strip()

print("extract annotation: OK")

# create tag
subprocess.check_output(["git", "tag", "-a", final_version, "-m", release_message, "--cleanup=whitespace"], stdin=None, stderr=None, shell=False)

print("        create tag: OK")

# push tag
subprocess.check_output(["git", "push", "--quiet", "origin", final_version], stdin=None, stderr=None, shell=False)
print("          push tag: OK")
