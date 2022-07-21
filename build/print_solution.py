import json

with open("solution.json", "r") as fh:
    solution_data = json.load(fh)
        
print(
f"""
AUTHORS={solution_data["authors"]}
COMPANY={solution_data["company"]}
COPYRIGHT={solution_data["copyright"]}
COPYRIGHT2={solution_data["copyright"]}
C2OPYRIGHT2={solution_data["company"]}
PRODUCT={solution_data["product"]}
PACKAGELICENSEEXPRESSION={solution_data["license"]}
PACKAGEPROJECTURL={solution_data["project-url"]}
REPOSITORYURL={solution_data["repository-url"]}
PACKAGEICONURL={solution_data["icon-url"]}
""")