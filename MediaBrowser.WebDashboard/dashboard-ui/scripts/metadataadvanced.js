﻿(function (window, $) {

    function loadAdvancedConfig(page, config) {

        $('#chkSaveMetadataHidden', page).checked(config.SaveMetadataHidden).checkboxradio("refresh");

        $('#txtMetadataPath', page).val(config.MetadataPath || '');

        $('#chkPeopleActors', page).checked(config.PeopleMetadataOptions.DownloadActorMetadata).checkboxradio("refresh");
        $('#chkPeopleComposers', page).checked(config.PeopleMetadataOptions.DownloadComposerMetadata).checkboxradio("refresh");
        $('#chkPeopleDirectors', page).checked(config.PeopleMetadataOptions.DownloadDirectorMetadata).checkboxradio("refresh");
        $('#chkPeopleProducers', page).checked(config.PeopleMetadataOptions.DownloadProducerMetadata).checkboxradio("refresh");
        $('#chkPeopleWriters', page).checked(config.PeopleMetadataOptions.DownloadWriterMetadata).checkboxradio("refresh");
        $('#chkPeopleOthers', page).checked(config.PeopleMetadataOptions.DownloadOtherPeopleMetadata).checkboxradio("refresh");
        $('#chkPeopleGuestStars', page).checked(config.PeopleMetadataOptions.DownloadGuestStarMetadata).checkboxradio("refresh");

        $('.chkEnableVideoFrameAnalysis', page).checked(config.EnableVideoFrameByFrameAnalysis);

        Dashboard.hideLoadingMsg();
    }

    function loadMetadataConfig(page, config) {

        $('#selectDateAdded', page).val((config.UseFileCreationTimeForDateAdded ? '1' : '0')).selectmenu("refresh");
    }

    function loadTmdbConfig(page, config) {

        $('#chkEnableTmdbUpdates', page).checked(config.EnableAutomaticUpdates).checkboxradio("refresh");
    }

    function loadTvdbConfig(page, config) {

        $('#chkEnableTvdbUpdates', page).checked(config.EnableAutomaticUpdates).checkboxradio("refresh");
    }

    function loadFanartConfig(page, config) {

        $('#chkEnableFanartUpdates', page).checked(config.EnableAutomaticUpdates).checkboxradio("refresh");
        $('#txtFanartApiKey', page).val(config.UserApiKey || '');
    }

    function loadChapters(page, config, providers) {

        if (providers.length) {
            $('.noChapterProviders', page).hide();
            $('.chapterDownloadSettings', page).show();
        } else {
            $('.noChapterProviders', page).show();
            $('.chapterDownloadSettings', page).hide();
        }

        $('#chkChaptersMovies', page).checked(config.EnableMovieChapterImageExtraction).checkboxradio("refresh");
        $('#chkChaptersEpisodes', page).checked(config.EnableEpisodeChapterImageExtraction).checkboxradio("refresh");
        $('#chkChaptersOtherVideos', page).checked(config.EnableOtherVideoChapterImageExtraction).checkboxradio("refresh");

        $('#chkDownloadChapterMovies', page).checked(config.DownloadMovieChapters).checkboxradio("refresh");
        $('#chkDownloadChapterEpisodes', page).checked(config.DownloadEpisodeChapters).checkboxradio("refresh");

        $('#chkExtractChaptersDuringLibraryScan', page).checked(config.ExtractDuringLibraryScan).checkboxradio("refresh");

        renderChapterFetchers(page, config, providers);

        Dashboard.hideLoadingMsg();
    }

    function renderChapterFetchers(page, config, plugins) {

        var html = '';

        if (!plugins.length) {
            $('.chapterFetchers', page).html(html).hide().trigger('create');
            return;
        }

        var i, length, plugin, id;

        html += '<div class="ui-controlgroup-label" style="margin-bottom:0;padding-left:2px;">';
        html += Globalize.translate('LabelChapterDownloaders');
        html += '</div>';

        html += '<div style="display:inline-block;width: 75%;vertical-align:top;">';
        html += '<div data-role="controlgroup" class="chapterFetcherGroup">';

        for (i = 0, length = plugins.length; i < length; i++) {

            plugin = plugins[i];

            id = 'chkChapterFetcher' + i;

            var isChecked = config.DisabledFetchers.indexOf(plugin.Name) == -1 ? ' checked="checked"' : '';

            html += '<input class="chkChapterFetcher" type="checkbox" name="' + id + '" id="' + id + '" data-pluginname="' + plugin.Name + '" data-mini="true"' + isChecked + '>';
            html += '<label for="' + id + '">' + plugin.Name + '</label>';
        }

        html += '</div>';
        html += '</div>';

        if (plugins.length > 1) {
            html += '<div style="display:inline-block;vertical-align:top;margin-left:5px;">';

            for (i = 0, length = plugins.length; i < length; i++) {

                html += '<div style="margin:6px 0;">';
                if (i == 0) {
                    html += '<button data-inline="true" disabled="disabled" class="btnUp" data-pluginindex="' + i + '" type="button" data-icon="arrow-u" data-mini="true" data-iconpos="notext" style="margin: 0 1px;">Up</button>';
                    html += '<button data-inline="true" class="btnDown" data-pluginindex="' + i + '" type="button" data-icon="arrow-d" data-mini="true" data-iconpos="notext" style="margin: 0 1px;">Down</button>';
                } else if (i == (plugins.length - 1)) {
                    html += '<button data-inline="true" class="btnUp" data-pluginindex="' + i + '" type="button" data-icon="arrow-u" data-mini="true" data-iconpos="notext" style="margin: 0 1px;">Up</button>';
                    html += '<button data-inline="true" disabled="disabled" class="btnDown" data-pluginindex="' + i + '" type="button" data-icon="arrow-d" data-mini="true" data-iconpos="notext" style="margin: 0 1px;">Down</button>';
                }
                else {
                    html += '<button data-inline="true" class="btnUp" data-pluginindex="' + i + '" type="button" data-icon="arrow-u" data-mini="true" data-iconpos="notext" style="margin: 0 1px;">Up</button>';
                    html += '<button data-inline="true" class="btnDown" data-pluginindex="' + i + '" type="button" data-icon="arrow-d" data-mini="true" data-iconpos="notext" style="margin: 0 1px;">Down</button>';
                }
                html += '</div>';
            }
        }

        html += '</div>';
        html += '<div class="fieldDescription">' + Globalize.translate('LabelChapterDownloadersHelp') + '</div>';

        var elem = $('.chapterFetchers', page).html(html).show().trigger('create');

        $('.btnDown', elem).on('click', function () {
            var index = parseInt(this.getAttribute('data-pluginindex'));

            var elemToMove = $('.chapterFetcherGroup .ui-checkbox', page)[index];

            var insertAfter = $(elemToMove).next('.ui-checkbox')[0];

            elemToMove.parentNode.removeChild(elemToMove);
            $(elemToMove).insertAfter(insertAfter);

            $('.chapterFetcherGroup', page).controlgroup('destroy').controlgroup();
        });

        $('.btnUp', elem).on('click', function () {

            var index = parseInt(this.getAttribute('data-pluginindex'));

            var elemToMove = $('.chapterFetcherGroup .ui-checkbox', page)[index];

            var insertBefore = $(elemToMove).prev('.ui-checkbox')[0];

            elemToMove.parentNode.removeChild(elemToMove);
            $(elemToMove).insertBefore(insertBefore);

            $('.chapterFetcherGroup', page).controlgroup('destroy').controlgroup();
        });
    }

    function onSubmit() {
        var form = this;

        Dashboard.showLoadingMsg();

        saveAdvancedConfig(form);
        saveChapters(form);
        saveMetadata(form);
        saveTmdb(form);
        saveTvdb(form);
        saveFanart(form);

        // Disable default form submission
        return false;
    }

    $(document).on('pageinit', "#advancedMetadataConfigurationPage", function () {

        var page = this;

        $('#btnSelectMetadataPath', page).on("click.selectDirectory", function () {

            var picker = new DirectoryBrowser(page);

            picker.show({

                callback: function (path) {

                    if (path) {
                        $('#txtMetadataPath', page).val(path);
                    }
                    picker.close();
                },

                header: Globalize.translate('HeaderSelectMetadataPath'),

                instruction: Globalize.translate('HeaderSelectMetadataPathHelp')
            });
        });

        $('.advancedMetadataConfigurationForm').on('submit', onSubmit).on('submit', onSubmit);


    }).on('pageshowready', "#advancedMetadataConfigurationPage", function () {

        var page = this;

        ApiClient.getServerConfiguration().done(function (configuration) {

            loadAdvancedConfig(page, configuration);

        });

        ApiClient.getNamedConfiguration("metadata").done(function (metadata) {

            loadMetadataConfig(page, metadata);

        });

        ApiClient.getNamedConfiguration("fanart").done(function (metadata) {

            loadFanartConfig(page, metadata);
        });

        ApiClient.getNamedConfiguration("themoviedb").done(function (metadata) {

            loadTmdbConfig(page, metadata);
        });

        ApiClient.getNamedConfiguration("tvdb").done(function (metadata) {

            loadTvdbConfig(page, metadata);
        });

        var promise1 = ApiClient.getNamedConfiguration("chapters");
        var promise2 = ApiClient.getJSON(ApiClient.getUrl("Providers/Chapters"));

        $.when(promise1, promise2).done(function (response1, response2) {

            loadChapters(page, response1[0], response2[0]);
        });
    });

    function saveFanart(form) {

        ApiClient.getNamedConfiguration("fanart").done(function (config) {

            config.EnableAutomaticUpdates = $('#chkEnableFanartUpdates', form).checked();
            config.UserApiKey = $('#txtFanartApiKey', form).val();

            ApiClient.updateNamedConfiguration("fanart", config);
        });
    }

    function saveTvdb(form) {

        ApiClient.getNamedConfiguration("tvdb").done(function (config) {

            config.EnableAutomaticUpdates = $('#chkEnableTvdbUpdates', form).checked();

            ApiClient.updateNamedConfiguration("tvdb", config);
        });
    }

    function saveTmdb(form) {

        ApiClient.getNamedConfiguration("themoviedb").done(function (config) {

            config.EnableAutomaticUpdates = $('#chkEnableTmdbUpdates', form).checked();

            ApiClient.updateNamedConfiguration("themoviedb", config);
        });
    }

    function saveAdvancedConfig(form) {

        ApiClient.getServerConfiguration().done(function (config) {

            config.SaveMetadataHidden = $('#chkSaveMetadataHidden', form).checked();

            config.EnableVideoFrameByFrameAnalysis = $('.chkEnableVideoFrameAnalysis', form).checked();

            config.EnableTvDbUpdates = $('#chkEnableTvdbUpdates', form).checked();
            config.EnableTmdbUpdates = $('#chkEnableTmdbUpdates', form).checked();
            config.EnableFanArtUpdates = $('#chkEnableFanartUpdates', form).checked();
            config.MetadataPath = $('#txtMetadataPath', form).val();
            config.FanartApiKey = $('#txtFanartApiKey', form).val();

            config.PeopleMetadataOptions.DownloadActorMetadata = $('#chkPeopleActors', form).checked();
            config.PeopleMetadataOptions.DownloadComposerMetadata = $('#chkPeopleComposers', form).checked();
            config.PeopleMetadataOptions.DownloadDirectorMetadata = $('#chkPeopleDirectors', form).checked();
            config.PeopleMetadataOptions.DownloadGuestStarMetadata = $('#chkPeopleGuestStars', form).checked();
            config.PeopleMetadataOptions.DownloadProducerMetadata = $('#chkPeopleProducers', form).checked();
            config.PeopleMetadataOptions.DownloadWriterMetadata = $('#chkPeopleWriters', form).checked();
            config.PeopleMetadataOptions.DownloadOtherPeopleMetadata = $('#chkPeopleOthers', form).checked();

            ApiClient.updateServerConfiguration(config).done(Dashboard.processServerConfigurationUpdateResult);
        });
    }

    function saveMetadata(form) {

        ApiClient.getNamedConfiguration("metadata").done(function (config) {

            config.UseFileCreationTimeForDateAdded = $('#selectDateAdded', form).val() == '1';

            ApiClient.updateNamedConfiguration("metadata", config);
        });
    }

    function saveChapters(form) {

        ApiClient.getNamedConfiguration("chapters").done(function (config) {

            config.EnableMovieChapterImageExtraction = $('#chkChaptersMovies', form).checked();
            config.EnableEpisodeChapterImageExtraction = $('#chkChaptersEpisodes', form).checked();
            config.EnableOtherVideoChapterImageExtraction = $('#chkChaptersOtherVideos', form).checked();

            config.DownloadMovieChapters = $('#chkDownloadChapterMovies', form).checked();
            config.DownloadEpisodeChapters = $('#chkDownloadChapterEpisodes', form).checked();
            config.ExtractDuringLibraryScan = $('#chkExtractChaptersDuringLibraryScan', form).checked();

            config.DisabledFetchers = $('.chkChapterFetcher:not(:checked)', form).get().map(function (c) {

                return c.getAttribute('data-pluginname');

            });

            config.FetcherOrder = $('.chkChapterFetcher', form).get().map(function (c) {

                return c.getAttribute('data-pluginname');

            });

            ApiClient.updateNamedConfiguration("chapters", config);
        });
    }

})(window, jQuery);

