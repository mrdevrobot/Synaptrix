# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

<a name="3.2.0"></a>
## [3.2.0](https://www.github.com/mrdevrobot/Synaptrix/releases/tag/v3.2.0) (2026-09-17)

### Features

* **generator:** closed pipeline-behavior registrations for AOT hosts ([9874017](https://www.github.com/mrdevrobot/Synaptrix/commit/987401700f448dac276427faf303dc20f22537cd))

<a name="3.1.3"></a>
## [3.1.3](https://www.github.com/mrdevrobot/Synaptrix/releases/tag/v3.1.3) (2026-08-07)

### Bug Fixes

* GeneratedMediator falls back to reflection-based Mediator instead of throwing ([d53acf8](https://www.github.com/mrdevrobot/Synaptrix/commit/d53acf84ec336fc13080395f2a841e5478b8d489))

<a name="3.1.2"></a>
## [3.1.2](https://www.github.com/mrdevrobot/Concordia/releases/tag/v3.1.2) (2026-08-07)

### Bug Fixes

* reject open-generic handlers whose interface slots aren't direct type-parameter references ([b0b484a](https://www.github.com/mrdevrobot/Concordia/commit/b0b484a7b029a320415e5b2fac3f549cae935a34))

<a name="3.1.1"></a>
## [3.1.1](https://www.github.com/mrdevrobot/Concordia/releases/tag/v3.1.1) (2026-08-07)

### Bug Fixes

* skip open-generic registration for asymmetric-arity handlers ([4e1f034](https://www.github.com/mrdevrobot/Concordia/commit/4e1f034d47c48210ba2188dab0138f28436d218b))

<a name="3.0.3"></a>
## [3.0.3](https://www.github.com/mrdevrobot/Concordia/releases/tag/v3.0.3) (2026-05-13)

### Bug Fixes

* Exclude open-generic handlers from concrete handler registrations ([fc2eb54](https://www.github.com/mrdevrobot/Concordia/commit/fc2eb54))

<a name="3.0.2"></a>
## [3.0.2](https://www.github.com/mrdevrobot/Concordia/releases/tag/v3.0.2) (2026-05-13)

### Bug Fixes

* Support open-generic handlers and pipeline dispatch ([b93e7ab](https://www.github.com/mrdevrobot/Concordia/commit/b93e7ab65b2fa913829436b1ac91d66bd181c214))

<a name="3.0.1"></a>
## [3.0.1](https://www.github.com/mrdevrobot/Concordia/releases/tag/v3.0.1) (2026-04-14)

### Bug Fixes

* Resolve handlers lazily via IServiceProvider ([d458227](https://www.github.com/mrdevrobot/Concordia/commit/d4582270368d262921e5c4b877cef677c0223db2))

<a name="3.0.0"></a>
## [3.0.0](https://www.github.com/mrdevrobot/Concordia/releases/tag/v3.0.0) (2026-04-12)

### Features

* Rebrand Concordia to Synaptrix across repo ([11b46ef](https://www.github.com/mrdevrobot/Concordia/commit/11b46ef5fa44d478581ab375d58fdaa1211ae9ba))

### Breaking Changes

* Rebrand Concordia to Synaptrix across repo ([11b46ef](https://www.github.com/mrdevrobot/Concordia/commit/11b46ef5fa44d478581ab375d58fdaa1211ae9ba))

<a name="2.4.1"></a>
## [2.4.1](https://www.github.com/mrdevrobot/Concordia/releases/tag/v2.4.1) (2026-04-08)

### Bug Fixes

* Add mediator fallback and update project files ([5f96ff4](https://www.github.com/mrdevrobot/Concordia/commit/5f96ff4a0d2a10fab19bbce7821528fd615fff43))

<a name="2.4.0"></a>
## [2.4.0](https://www.github.com/mrdevrobot/Concordia/releases/tag/v2.4.0) (2026-04-05)

### Features

* Add benchmarks and update mediator/generator ([c80617d](https://www.github.com/mrdevrobot/Concordia/commit/c80617d1d16477c81f0ab5a974d326bc000426bc))

<a name="2.3.3"></a>
## [2.3.3](https://www.github.com/mrdevrobot/Concordia/releases/tag/v2.3.3) (2026-02-02)

### Features

* adds background parallel publisher ([8d65323](https://www.github.com/mrdevrobot/Concordia/commit/8d65323dbb017cb5b7ac075d447cee1b81472d6a))
* adds default pre and post behavior and task when all publisher ([9ff508f](https://www.github.com/mrdevrobot/Concordia/commit/9ff508f1077dcb2d2910e68a848f394331a6d944))
* adds support to net9.0 ([4e9d9f5](https://www.github.com/mrdevrobot/Concordia/commit/4e9d9f5fc4626c39edc2facc50419ea4cf879747))
* Implement DiscoverConcordiaHandlersAttribute, recursive discovery, and auto-injection ([6327d2d](https://www.github.com/mrdevrobot/Concordia/commit/6327d2d63f5f2bf2c541acd1be3601159de5a038))
* net10 ([86e47a9](https://www.github.com/mrdevrobot/Concordia/commit/86e47a9a9f09e5b488a763afe3cc5060136847bb))
* setup Jekyll documentation and add generator tests ([c1f4da5](https://www.github.com/mrdevrobot/Concordia/commit/c1f4da577fdca44c2595652b7f9ce24487df14bb))
* supports the send on dynamic objects ([c2b676b](https://www.github.com/mrdevrobot/Concordia/commit/c2b676b3cc9b6d765fd2a29884c7a4fea88a2e4a))
* **examples:** add shared examples project and ProductsController ([4701fa5](https://www.github.com/mrdevrobot/Concordia/commit/4701fa5768c247c7f5771aa01b68223d1b296150))

### Bug Fixes

* adds cancellation token to next call for pipelines ([0d1237b](https://www.github.com/mrdevrobot/Concordia/commit/0d1237b21a364f8a8434602b0ae6c1b2b34a2d4a))
* adds Generated suffix to generated files namespace to avoid uninteded using ([74838d2](https://www.github.com/mrdevrobot/Concordia/commit/74838d28e49303a434fe8b7c33ae791618b64f6b))
* corrects generator csproj for nuget ([4d79fa2](https://www.github.com/mrdevrobot/Concordia/commit/4d79fa2fc9daaeaa45e781e10b1c8288b31232fc))
* corrects generator handlers identification ([cb13d19](https://www.github.com/mrdevrobot/Concordia/commit/cb13d1990c6fd6affc29fa52d60b0e5e59ddbf06))
* corrects reamde ([b6bddf5](https://www.github.com/mrdevrobot/Concordia/commit/b6bddf5ebecdde8df514ab4ff3267024d998da3e))
* corrects the generator ([84158d6](https://www.github.com/mrdevrobot/Concordia/commit/84158d65291dcf7f4c3e61dd0b47544fd8703bfb))
* corrects the generator ([463f0ea](https://www.github.com/mrdevrobot/Concordia/commit/463f0eaf5c53423e9e7f9240b5d6d3081bfdc15b))
* move contracts to Concordia namespace ([bfe98df](https://www.github.com/mrdevrobot/Concordia/commit/bfe98df2c889fe0faca1b31057687dcc41abb22d))
* recursive namespace correction ([479c495](https://www.github.com/mrdevrobot/Concordia/commit/479c495b6b13b965ff3027eeac1b63888112180c))
* update docs version to 2.3.0 and enable main branch deploy ([851a80d](https://www.github.com/mrdevrobot/Concordia/commit/851a80d8665e201544e44d4fdf038388cba9274a))

### Breaking Changes

* net10 ([86e47a9](https://www.github.com/mrdevrobot/Concordia/commit/86e47a9a9f09e5b488a763afe3cc5060136847bb))

<a name="2.3.3"></a>
## [2.3.3](https://www.github.com/mrdevrobot/Concordia/releases/tag/v2.3.3) (2026-02-02)

<a name="2.3.2"></a>
## [2.3.2](https://www.github.com/mrdevrobot/Concordia/releases/tag/v2.3.2) (2026-02-02)

### Bug Fixes

* adds Generated suffix to generated files namespace to avoid uninteded using ([74838d2](https://www.github.com/mrdevrobot/Concordia/commit/74838d28e49303a434fe8b7c33ae791618b64f6b))

<a name="2.3.1"></a>
## [2.3.1](https://www.github.com/mrdevrobot/Concordia/releases/tag/v2.3.1) (2026-02-01)

<a name="2.3.0"></a>
## [2.3.0](https://www.github.com/mrdevrobot/Concordia/releases/tag/v2.3.0) (2026-02-01)

### Features

* setup Jekyll documentation and add generator tests ([c1f4da5](https://www.github.com/mrdevrobot/Concordia/commit/c1f4da577fdca44c2595652b7f9ce24487df14bb))

<a name="2.2.0"></a>
## [2.2.0](https://www.github.com/mrdevrobot/Concordia/releases/tag/v2.2.0) (2026-02-01)

### Features

* **examples:** add shared examples project and ProductsController ([4701fa5](https://www.github.com/mrdevrobot/Concordia/commit/4701fa5768c247c7f5771aa01b68223d1b296150))

<a name="2.1.0"></a>
## [2.1.0](https://www.github.com/mrdevrobot/Concordia/releases/tag/v2.1.0) (2026-01-31)

### Features

* Implement DiscoverConcordiaHandlersAttribute, recursive discovery, and auto-injection ([6327d2d](https://www.github.com/mrdevrobot/Concordia/commit/6327d2d63f5f2bf2c541acd1be3601159de5a038))

<a name="2.0.0"></a>
## [2.0.0](https://www.github.com/lucafabbri/Concordia/releases/tag/v2.0.0) (2025-12-30)

### Features

* net10 ([86e47a9](https://www.github.com/lucafabbri/Concordia/commit/86e47a9a9f09e5b488a763afe3cc5060136847bb))

### Breaking Changes

* net10 ([86e47a9](https://www.github.com/lucafabbri/Concordia/commit/86e47a9a9f09e5b488a763afe3cc5060136847bb))

<a name="1.3.0"></a>
## [1.3.0](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.3.0) (2025-11-10)

### Features

* adds background parallel publisher ([8d65323](https://www.github.com/lucafabbri/Concordia/commit/8d65323dbb017cb5b7ac075d447cee1b81472d6a))

<a name="1.2.6"></a>
## [1.2.6](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.2.6) (2025-10-06)

### Bug Fixes

* corrects generator handlers identification ([cb13d19](https://www.github.com/lucafabbri/Concordia/commit/cb13d1990c6fd6affc29fa52d60b0e5e59ddbf06))

<a name="1.2.5"></a>
## [1.2.5](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.2.5) (2025-09-05)

<a name="1.2.4"></a>
## [1.2.4](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.2.4) (2025-09-05)

### Bug Fixes

* corrects generator csproj for nuget ([4d79fa2](https://www.github.com/lucafabbri/Concordia/commit/4d79fa2fc9daaeaa45e781e10b1c8288b31232fc))

<a name="1.2.3"></a>
## [1.2.3](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.2.3) (2025-09-05)

<a name="1.2.2"></a>
## [1.2.2](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.2.2) (2025-09-03)

<a name="1.2.1"></a>
## [1.2.1](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.2.1) (2025-09-03)

### Bug Fixes

* corrects reamde ([b6bddf5](https://www.github.com/lucafabbri/Concordia/commit/b6bddf5ebecdde8df514ab4ff3267024d998da3e))
* corrects the generator ([84158d6](https://www.github.com/lucafabbri/Concordia/commit/84158d65291dcf7f4c3e61dd0b47544fd8703bfb))
* corrects the generator ([463f0ea](https://www.github.com/lucafabbri/Concordia/commit/463f0eaf5c53423e9e7f9240b5d6d3081bfdc15b))

<a name="1.2.0"></a>
## [1.2.0](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.2.0) (2025-08-31)

### Features

* adds default pre and post behavior and task when all publisher ([9ff508f](https://www.github.com/lucafabbri/Concordia/commit/9ff508f1077dcb2d2910e68a848f394331a6d944))

### Bug Fixes

* adds cancellation token to next call for pipelines ([0d1237b](https://www.github.com/lucafabbri/Concordia/commit/0d1237b21a364f8a8434602b0ae6c1b2b34a2d4a))
* move contracts to Concordia namespace ([bfe98df](https://www.github.com/lucafabbri/Concordia/commit/bfe98df2c889fe0faca1b31057687dcc41abb22d))

<a name="1.1.0"></a>
## [1.1.0](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.1.0) (2025-08-30)

### Features

* adds support to net9.0 ([4e9d9f5](https://www.github.com/lucafabbri/Concordia/commit/4e9d9f5fc4626c39edc2facc50419ea4cf879747))

<a name="1.0.1"></a>
## [1.0.1](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.0.1) (2025-07-30)

<a name="1.0.0"></a>
## [1.0.0](https://www.github.com/lucafabbri/Concordia/releases/tag/v1.0.0) (2025-07-30)

### Features

* supports the send on dynamic objects ([c2b676b](https://www.github.com/lucafabbri/Concordia/commit/c2b676b3cc9b6d765fd2a29884c7a4fea88a2e4a))

