﻿define([], function () {

    return function (page, providerId, options) {

        var self = this;

        var listingsId;

        function reload() {

            Dashboard.showLoadingMsg();

            ApiClient.getNamedConfiguration("livetv").done(function (config) {

                var info = config.ListingProviders.filter(function (i) {
                    return i.Id == providerId;
                })[0] || {};

                listingsId = info.ListingsId;
                $('#selectListing', page).val(info.ListingsId || '').selectmenu('refresh');
                page.querySelector('.txtUser').value = info.Username || '';
                page.querySelector('.txtPass').value = info.Username || '';

                page.querySelector('.txtZipCode').value = info.ZipCode || '';

                setCountry(info);
            });
        }

        function setCountry(info) {

            ApiClient.getJSON(ApiClient.getUrl('LiveTv/ListingProviders/SchedulesDirect/Countries')).done(function (result) {

                var countryList = [];
                var i, length;

                for (var region in result) {
                    var countries = result[region];

                    if (countries.length && region !== 'ZZZ') {
                        for (i = 0, length = countries.length; i < length; i++) {
                            countryList.push({
                                name: countries[i].fullName,
                                value: countries[i].shortName
                            });
                        }
                    }
                }

                countryList.sort(function (a, b) {
                    if (a.name > b.name) {
                        return 1;
                    }
                    if (a.name < b.name) {
                        return -1;
                    }
                    // a must be equal to b
                    return 0;
                });

                $('#selectCountry', page).html(countryList.map(function (c) {

                    return '<option value="' + c.value + '">' + c.name + '</option>';

                }).join('')).val(info.Country || '').selectmenu('refresh');

                $(page.querySelector('.txtZipCode')).trigger('change');

            }).fail(function () {

                Dashboard.alert({
                    message: Globalize.translate('ErrorGettingTvLineups')
                });
            });

            Dashboard.hideLoadingMsg();
        }

        function submitLoginForm() {

            Dashboard.showLoadingMsg();

            var info = {
                Type: 'SchedulesDirect',
                Username: page.querySelector('.txtUser').value,
                Password: CryptoJS.SHA1(page.querySelector('.txtPass').value).toString()
            };

            var id = providerId;

            if (id) {
                info.Id = id;
            }

            ApiClient.ajax({
                type: "POST",
                url: ApiClient.getUrl('LiveTv/ListingProviders', {
                    ValidateLogin: true
                }),
                data: JSON.stringify(info),
                contentType: "application/json"

            }).done(function (result) {

                Dashboard.processServerConfigurationUpdateResult();
                providerId = result.Id;
                reload();

            }).fail(function () {
                Dashboard.alert({
                    message: Globalize.translate('ErrorSavingTvProvider')
                });
            });

        }

        function submitListingsForm() {

            var selectedListingsId = $('#selectListing', page).val();

            if (!selectedListingsId) {
                Dashboard.alert({
                    message: Globalize.translate('ErrorPleaseSelectLineup')
                });
                return;
            }

            Dashboard.showLoadingMsg();

            var id = providerId;

            ApiClient.getNamedConfiguration("livetv").done(function (config) {

                var info = config.ListingProviders.filter(function (i) {
                    return i.Id == id;
                })[0];

                info.ZipCode = page.querySelector('.txtZipCode').value;
                info.Country = $('#selectCountry', page).val();
                info.ListingsId = selectedListingsId;

                ApiClient.ajax({
                    type: "POST",
                    url: ApiClient.getUrl('LiveTv/ListingProviders', {
                        ValidateListings: true
                    }),
                    data: JSON.stringify(info),
                    contentType: "application/json"

                }).done(function (result) {

                    Dashboard.hideLoadingMsg();
                    if (options.showConfirmation !== false) {
                        Dashboard.processServerConfigurationUpdateResult();
                    }
                    $(self).trigger('submitted');

                }).fail(function () {
                    Dashboard.hideLoadingMsg();
                    Dashboard.alert({
                        message: Globalize.translate('ErrorSavingTvProvider')
                    });
                });

            });
        }

        function refreshListings(value) {

            if (!value) {
                $('#selectListing', page).html('').selectmenu('refresh');
                return;
            }

            Dashboard.showModalLoadingMsg();

            ApiClient.ajax({
                type: "GET",
                url: ApiClient.getUrl('LiveTv/ListingProviders/Lineups', {
                    Id: providerId,
                    Location: value,
                    Country: $('#selectCountry', page).val()
                }),
                dataType: 'json'

            }).done(function (result) {

                $('#selectListing', page).html(result.map(function (o) {

                    return '<option value="' + o.Id + '">' + o.Name + '</option>';

                })).selectmenu('refresh');

                if (listingsId) {
                    $('#selectListing', page).val(listingsId).selectmenu('refresh');
                }

                Dashboard.hideModalLoadingMsg();

            }).fail(function (result) {

                Dashboard.alert({
                    message: Globalize.translate('ErrorGettingTvLineups')
                });
                refreshListings('');
                Dashboard.hideModalLoadingMsg();
            });
        }

        self.submit = function () {
            page.querySelector('.btnSubmitListingsContainer').click();
        };

        self.init = function () {

            options = options || {};

            if (options.showCancelButton !== false) {
                page.querySelector('.btnCancel').classList.remove('hide');
            } else {
                page.querySelector('.btnCancel').classList.add('hide');
            }

            if (options.showSubmitButton !== false) {
                page.querySelector('.btnSubmitListings').classList.remove('hide');
            } else {
                page.querySelector('.btnSubmitListings').classList.add('hide');
            }

            $('.formLogin', page).on('submit', function () {
                submitLoginForm();
                return false;
            });

            $('.formListings', page).on('submit', function () {
                submitListingsForm();
                return false;
            });

            $('.txtZipCode', page).on('change', function () {
                refreshListings(this.value);
            });

            $('.createAccountHelp', page).html(Globalize.translate('MessageCreateAccountAt', '<a href="http://www.schedulesdirect.org" target="_blank">http://www.schedulesdirect.org</a>'));

            reload();
        };
    }
});