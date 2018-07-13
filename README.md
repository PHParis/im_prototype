# Welcome

im_prototype purpose is to interlink datasets from Linked Data.

## Getting Started

You can use Docker to reproduce the experiments (easiest way):

1. `docker build -t prototype .`
2. `docker run -v ${PWD}/data:/data prototype`

Alternatively, you can directly use the `dotnet` command if installed to run the application:

1. `cd ./Prototype`
2. `dotnet run`

Configuration file is ready to use with Docker. You will have to adapt values to work with the `dotnet` command.

## Configuration

To configure the prototype, you can modify values in the *appsettings.json* file (in the *Prototype* directory). Especially, you can queue several interlinking tasks in the `SourceTargetCouples` array. Below is an an example of the configuration for the SPIMBENCH task from [OAEI 2017](http://islab.di.unimi.it/content/im_oaei/2017/):

```json
{
  "rdfFile1": "/data/SPIMBENCH_small/source.ttl",
  "rdfFile2": "/data/SPIMBENCH_small/target.ttl",
  "refalign": "/data/SPIMBENCH_small/refalign.rdf",
  "Name": "SPIMBENCH_small 2017",
  "classesToSelect1": [
    "http://www.bbc.co.uk/ontologies/creativework/CreativeWork"
  ],
  "classesToSelect2": [
    "http://www.bbc.co.uk/ontologies/creativework/CreativeWork"
  ],
  "Active": false
}
```

`rdfFile1` and `rdfFile2` are source and target file paths. `refalign` is the path to the gold standard file. `Name` is the name you want to give to the task. `classesToSelect1` and `classesToSelect2` are the names of the classes to which the source and target instances must belong (respectively). If the value `Active` is `false` then the task will be skipped.