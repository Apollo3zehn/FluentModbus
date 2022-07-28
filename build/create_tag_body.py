import os
import re
import subprocess

tag = os.getenv('GITHUB_REF_NAME')

if tag is None:
    raise Exception("GITHUB_REF_NAME is not defined")

os.mkdir("artifacts")

with open("artifacts/tag_body.txt", "w") as file:

    output = subprocess.check_output(["git", "tag", "-l", "--format='%(contents)'", tag], stdin=None, stderr=None, shell=False)
    match = re.search("'(.*)'", output.decode("utf8"), re.DOTALL)

    if match is None:
        raise Exception("Unable to extract the tag body")

    tag_body = str(match.groups(1)[0])
    file.write(tag_body)


