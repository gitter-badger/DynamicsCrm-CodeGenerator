# DynamicsCrm-CodeGenerator
### Version: 6.15.4
---

A Visual Studio extension that allows generating early bound classes for Microsoft Dynamics CRM entities based on a template file, similar to Entity Framework.

### Features

  + Metadata
  + Web service contracts
	+ Supports exclusion of saving certain values
  + CRM Actions concrete classes
  + Filtering of attributes to reduce size
  + Bulk relation loading
  + Filtering on relation loading
  + Use display names of entities and fields as variable names
  + Locking names to avoid code errors on regeneration

### Benefits of using this tool over the standard tool

  + Control which entities to generate classes for this will keep the size of the generated code to a minimum.
    + If you use the CrmSvcUtil.exe to generate, the code file will be 200,000 lines, compared to ~1000 lines for each entity you select.
  + Customize the way the code is generated
    + You get a default T4 template for the code that is generated, which gives full control over how the code is generated.
  + Built for Visual Studio
    + You never have to leave Visual Studio to regenerate the classes.
	+ All the configurations* are stored in the project which allows you save them to Source Control.

## How To Use
I will post a complete guide soon ...

Install the VS extension.

#### Add a template to your project
Highlight the project where you want to store the template and generated code.   
Tools –> Add CRM Code Generator Template... (if you don't see this menu, then shutdown VS and reinstall the extension)

![File](Documentation/image_thumb_2.png)

  + Start with one of the provided templates.
  + After a template is added to your project you will be prompted for CRM connection info.
  + Pick the entities that you want to include.
  + Click the "Generate Code" button.

If you make schema changes in CRM and you want to refresh the code, right click the template and select "Run Custom Tool".

![File](Documentation/image_thumb_1.png)

#### Changing the template
When you make changes to the template and save, Visual Studio will automatically attempt to re-generate the code.

### Credits

  + Base code:
	+ Eric Labashosky
	+ https://github.com/xairrick/CrmCodeGenerator
	+ My work:
		+ Reworked the screens
		+ Added caching
		+ Added a lot of new features

---
**Copyright &copy; by Ahmed el-Sawalhy ([YagaSoft](http://yagasoft.com))** -- _GPL v3 Licence_
