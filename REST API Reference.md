iChen® 4.1 REST API Reference
=============================

Copyright © Chen Hsong Holdings Ltd.  All rights reserved.  
Document Version: 4.1  


Introduction
------------

A REST API is exposed by the iChen® Server 4.1 for advanced data 
management purposes. Third party software can utilize this REST API by making 
standard HTTP calls to the listening port (default 5757). 

All returned data, as well as all request payload data, are in standard 
**JSON** format. 


API Security
------------

The REST API is secured and can only be accessed by users with admin 
privileges. The user must first login to the iChen® Server 4.1 via the [Login API](#login) 
before any other REST API can be used. 

When logging in, a valid password must be provided. A user account with this 
password must exist on the server for this login operation to succeed. In 
addition, the account must have administrative authority (i.e. its valid 
authorization [filters](http://cloud.chenhsong.com/ichen/doc/code/enums.html#Filters) must be "All"). 

When iChen® Server 4.1 responds to a successful login attempt, a cookie 
is included within the response header containing a unique ID for the login 
session. This cookie must be included in any subsequent REST API calls. 
Calling a REST API without a valid session ID cookie will always get back a 
401 (Unauthorized) error. 

The login session automatically times-out after an interval of inactivity 
(default 15 minutes). When a login session is invalidated due to time-out, the 
user is automatically logged out of the server and must initiate the login 
process once again before continuing to use the REST API. 


Authentication
==============

Login
-----

> `POST http://`*iChen server:Port*`/login`

Use this API, providing a valid password, in order to log into the server. A 
user account with this password must exist on the server for the login 
operation to succeed. In addition, the account must have administrative 
authority (i.e. its valid authorization [filters](http://cloud.chenhsong.com/ichen/doc/code/enums.html#Filters) 
must be "All"). A cookie is included with the server's response header that 
contains a unique ID for this login session. 

Make sure that this cookie is sent in any subsequent REST API calls. Calling a 
REST API without a valid session ID results in a 401 (Unauthorized) error. 

If the login operation fails for any reason (e.g. invalid password, or the 
user account with the specified password does not have adequate [filters](http://cloud.chenhsong.com/ichen/doc/code/enums.html#Filters) 
authority), a 401 (Unauthorized) error is returned. 


### Payload

| JSON Property | Type     | Description                       |
|---------------|:--------:|-----------------------------------|
| `password`    | `string` | Password to log on to the server. |

#### Payload Example

~~~~~~~~~~~~json
{ "password":"helloworld" }
~~~~~~~~~~~~

### Returns

|JSON Property       |Type      |           Description                  |
|:-------------------|:--------:|:---------------------------------------|
|`sessionId`         |`string`  |Unique ID of this login session         |
|`started`           |`Date`    |Date/time when this login session started (in ISO-8601 format)|
|`lastAccessed`      |`Date`    |Date/time that this login session last had activity (in ISO-8601 format)|
|`id`                |`number`  |Unique ID of the logged-in user         |
|`name`              |`string`  |Name of the logged-in user              |
|`password`          |`string`  |Unique password of the logged-in user   |
|`isEnabled`         |`boolean` |Is this user account enabled?           |
|`filters`           |`string`  |A filter (if any) containing what type(s) of messages to receive|
|`accessLevel`       |`number`  |The allowed access level (0-10) for the logged-in user|
|`created`           |`Date`    |Date/time that this user account has been created|
|`modified`          |`Date`    |Date/time (if any) that this user account was last modified|

### Filters

The `filters` property is serialized to JSON as a *comma-separated* string
containing the type(s) of messages interested in receiving:

|Filter      |Message Type                       |Message Class(es) Affected|
|:----------:|:----------------------------------|:-----------------------:|
|`None`      |Nothing                            |*N/A*                    |
|`Status`    |Controller status and variables    |[`ControllerStatusMessage`](#controllerstatusmessage)|
|`Cycle`     |Cycle data                         |[`CycleDataMessage`](#cycledatamessage)|
|`Mold`      |Mold settings                      |[`MoldDataMessage`](#molddatamessage)|
|`Actions`   |Current action                     |[`ControllerActionMessage`](#controlleractionmessage)|
|`Alarms`    |Controller alarms                  |[`ControllerStatusMessage`](#controllerstatusmessage)|
|`Audit`     |Audit trail of setting changes     |[`ControllerStatusMessage`](#controllerstatusmessage)|
|`All`       |All message types                  |All message classes above|
|`JobCards`  |Job card-related messages          |[`RequestJobCardsListMessage`](#requestjobcardslistmessage), [`JobCardsListMessage`](#jobcardslistmessage)|
|`Operators` |Operator-related messages          |[`LoginOperatorMessage`](#loginoperatormessage), [`OperatorInfoMessage`](#operatorinfomessage)|
|`OPCUA`     |OPC UA communications              |*N/A*                    |

### Example

~~~~~~~~~~~~
POST http://iChen:5757/login
~~~~~~~~~~~~
~~~~~~~~~~~~json
{
  "sessionId": "790AD2ED9F444263AB4861FC0320443C",
  "started": "2016-05-21T22:21:40+08:00",
  "lastAccessed": "2016-05-21T22:21:40+08:00",
  "id": 888,
  "password": "helloworld",
  "name": "admin",
  "isEnabled": true,
  "filters": "All",
  "created": "2016-05-21T21:48:43+08:00"
}
~~~~~~~~~~~~


Logout
------

> `GET http://`*iChen server:Port*`/logout`



Server Management
=================

Get Server Status
-----------------

> `GET http://`*iChen server:Port*`/status`

### Returns

|JSON Property       |Type      |           Description                  |
|:-------------------|:--------:|:---------------------------------------|
|`started`           |`string`  |Date/time that the server started (in ISO-8601 format)|
|`uptime`            |`string`  |Length of time that the server has been running|
|`isRunning`         |`boolean` |Is the server currently running         |
|`version`           |`string`  |Version of the server                   |
|`environment`       |`string`  |Operating system that the server is running on|
|`port`              |`number`  |HTTP port that the server is listening on|
|`numClients`        |`number`  |Number of clients currently connected to the server|
|`numControllers`    |`number`  |Number of machines currently connected to the server|
|`clients`           |`object`  |A JSON hash containing a list of all the connected clients|
|`controllers`       |`object`  |A JSON hash containing a list of all the connected machines|

### Example

~~~~~~~~~~~~
http://iChen:5757/status
~~~~~~~~~~~~

~~~~~~~~~~~~json
{
  "started":"2016-03-29T18:07:44+08:00",
  "uptime":"12:23:34",
  "isRunning":true,
  "version":"4.1",
  "environment":"Microsoft Windows NT 10.0.10586.0",
  "port":5757,
  "numClients":1,
  "numControllers":2,
  "clients":{
    "192.2.3.45:9876":"admin (10) [Cycle, Audit, Alarms]"
  },
  "controllers":{
    "192.1.2.33:8800":"Machine #1 (123456) [JM138Ai]",
    "192.1.2.33:8801":"Machine #2 (98765) [EM120-V]"
  }
}
~~~~~~~~~~~~


Molds Management
================

Mold Object
-----------

### Object Definition

|JSON Property       |Type      |           Description                  |
|:-------------------|:--------:|:---------------------------------------|
|`id`                |`number`  |Unique numeric ID for this mold data set|
|`guid`              |`string`  |Unique textual ID for this mold data set|
|`name`              |`string`  |Unique name for this mold               |
|`controllerId`      |`number`  |Unique numeric ID for the machine that saved this mold data set|
|`isEnabled`         |`boolean` |Whether this mold data set should be used (or not)|
|`created`           |`string`  |Date/time that this mold data set was first saved (in ISO-8601 format)|
|`modified`          |`string`  |Date/time that this mold data set was last updated (in ISO-8601 format)|

### Example

~~~~~~~~~~~~json
{
  "id":88,
  "guid":"63019c56-04f2-4c7b-b8ca-9737386f333f",
  "name":"MOLD-888",
  "controllerId":98765,
  "isEnabled":true,
  "created":"2016-03-29T18:07:44+08:00"
}
~~~~~~~~~~~~


Mold Object With Settings Data
------------------------------

### Object Definition

|JSON Property       |Type      |           Description                  |
|:-------------------|:--------:|:---------------------------------------|
|`id`                |`number`  |Unique numeric ID for this mold data set|
|`guid`              |`string`  |Unique textual ID for this mold data set|
|`name`              |`string`  |Unique name for this mold               |
|`controllerId`      |`number`  |Unique numeric ID for the machine that saved this mold data set|
|`isEnabled`         |`boolean` |Whether this mold data set should be used (or not)|
|`numSettings`       |`number`  |Number of variables in this mold data set|
|`settings`          |`number[]`|A binary array of encoded variable values|
|`created`           |`string`  |Date/time that this mold data set was first saved (in ISO-8601 format)|
|`modified`          |`string`  |Date/time that this mold data set was last updated (in ISO-8601 format)|

### Note

Due to the large size of mold data sets, variable values are compressed and 
encoded into a binary array. Do not attempt to interpret this array or change 
it in any way or risk data not being able to round-trip. 

### Example

~~~~~~~~~~~~json
{
  "id":88,
  "guid":"63019c56-04f2-4c7b-b8ca-9737386f333f",
  "name":"MOLD-888",
  "controllerId":98765,
  "isEnabled":true,
  "numSettings":456,
  "settings":[65536,9876,65536,42,1,0],
  "created":"2016-03-29T18:07:44+08:00"
}
~~~~~~~~~~~~


Get a List of All the Molds
---------------------------

> `GET http://`*iChen server:Port*`/config/molds`

### Returns

An JSON object hash, with keys being the unique name for each mold, values 
being JSON object hashes with keys being the unique ID's for each machine and 
values being [Mold Objects](#mold-object). 

### Example

~~~~~~~~~~~~
http://iChen:5757/config/molds
~~~~~~~~~~~~

~~~~~~~~~~~~json
{
  "MOLD-123":{
    "123456":{
      "id":123,
      "guid":"7bbe0b99-d55d-4a79-8144-74cdcdea8345",
      "name":"MOLD-123",
      "controllerId":123456,
      "isEnabled":true,
      "created":"2016-03-29T18:07:44+08:00"
    },
    "98765":{
      "id":42,
      "guid":"eddb73ae-32a6-4b97-a57c-cb692b626466",
      "name":"MOLD-123",
      "controllerId":98765,
      "isEnabled":true,
      "created":"2016-03-29T18:07:44+08:00"
    }
  },
  "MOLD-888":{
    "98765":{
      "id":88,
      "guid":"63019c56-04f2-4c7b-b8ca-9737386f333f",
      "name":"MOLD-888",
      "controllerId":98765,
      "isEnabled":true,
      "created":"2016-03-29T18:07:44+08:00"
    }
  }
}
~~~~~~~~~~~~


Get a List of all the Molds Saved by a Particular Machine
---------------------------------------------------------

> `GET http://`*iChen server:Port*`/config/controllers/`*machine ID*`/molds` 

### Returns

A JSON object hash with keys being the unique names for each mold and values 
being [Mold Objects](#mold-object). 


### Example

~~~~~~~~~~~~
http://iChen:5757/config/controllers/123456/molds
~~~~~~~~~~~~

~~~~~~~~~~~~json
{
  "MOLD-123":{
    "id":123,
    "guid":"7bbe0b99-d55d-4a79-8144-74cdcdea8345",
    "name":"MOLD-123",
    "controllerId":123456,
    "isEnabled":true,
    "created":"2016-03-29T18:07:44+08:00"
  },
  "MOLD-888":{
    "id":88,
    "guid":"63019c56-04f2-4c7b-b8ca-9737386f333f",
    "name":"MOLD-888",
    "controllerId":123456,
    "isEnabled":true,
    "created":"2016-03-29T18:07:44+08:00"
  }
}
~~~~~~~~~~~~


Get a Particular Mold Data Set by ID
------------------------------------

> `GET http://`*iChen server:Port*`/config/molds/`*mold-id*

### Returns

A single [Mold Object With Settings Data](#mold-object-with-settings-data) 
with the specified unique numeric ID. 

### Example

~~~~~~~~~~~~
http://iChen:5757/config/molds/123
~~~~~~~~~~~~

~~~~~~~~~~~~json
{
  "id":123,
  "guid":"7bbe0b99-d55d-4a79-8144-74cdcdea8345",
  "name":"MOLD-123",
  "controllerId":123456,
  "isEnabled":true,
  "numSettings":456,
  "settings":[65536,9876,65536,42,1,0],
  "created":"2016-03-29T18:07:44+08:00"
}
~~~~~~~~~~~~


Add a Mold Data Set
-------------------

> `POST http://`*iChen server:Port*`/config/molds

### Payload

A single [Mold Object With Settings Data](#mold-object-with-settings-data) in 
JSON format. 

* The `id` property will be ignored and a new unique ID will be assigned to 
  the mold data set. 

* The `numSettings` property will be ignored. 

* The `guid` property, if omitted, will be assigned a new one. 

* The `isEnabled` property, if omitted, will be assumed `true`. 

* The `created` property, if omitted, will be set to the current date/time. 

#### Payload Example

~~~~~~~~~~~~json
{
  "name":"MOLD-123",
  "controllerId":123456,
  "settings":[65536,9876,65536,42,1,0]
}
~~~~~~~~~~~~

### Returns

A single [Mold Object With Settings Data](#mold-object-with-settings-data) 
that has been added. 

### Example

~~~~~~~~~~~~
POST http://iChen:5757/config/molds
~~~~~~~~~~~~
~~~~~~~~~~~~json
{
  "id":123,
  "guid":"7bbe0b99-d55d-4a79-8144-74cdcdea8345",
  "name":"MOLD-123",
  "controllerId":123456,
  "isEnabled":true,
  "numSettings":456,
  "settings":[65536,9876,65536,42,1,0],
  "created":"2016-03-29T18:07:44+08:00"
}
~~~~~~~~~~~~


Delete a Particular Mold Data Set by ID
---------------------------------------

> `DELETE http://`*iChen server:Port*`/config/molds/`*mold-id*

### Returns

A single [Mold Object With Settings Data](#mold-object-with-settings-data) 
that has been deleted. 

### Example

~~~~~~~~~~~~
DELETE http://iChen:5757/config/molds/123
~~~~~~~~~~~~
~~~~~~~~~~~~json
{
  "id":123,
  "guid":"7bbe0b99-d55d-4a79-8144-74cdcdea8345",
  "name":"MOLD-123",
  "controllerId":123456,
  "isEnabled":true,
  "numSettings":456,
  "settings":[65536,9876,65536,42,1,0],
  "created":"2016-03-29T18:07:44+08:00"
}
~~~~~~~~~~~~
