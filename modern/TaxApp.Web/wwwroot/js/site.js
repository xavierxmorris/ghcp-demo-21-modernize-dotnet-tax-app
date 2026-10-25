(function ($) {
    'use strict';

    // Live preview on the estimator page. It calls the JSON endpoint and reads PascalCase property names,
    // so the JSON shape is part of the app's contract.
    var $box = $('[data-quick-estimate]');
    if (!$box.length) {
        return;
    }

    var url = $box.data('quick-estimate');
    var $result = $box.find('[data-quick-result]');
    var timer = null;

    function refresh() {
        var income = $('#Income').val();
        if (!income) {
            $result.text('enter an income above');
            return;
        }

        $.getJSON(url, {
            taxYear: $('#TaxYear').val(),
            income: income,
            deductions: $('#Deductions').val() || 0
        }).done(function (estimate) {
            $result.text('total tax $' + Number(estimate.TotalTax).toFixed(2) +
                ' (' + Number(estimate.EffectiveRate).toFixed(2) + '% effective)');
        }).fail(function () {
            $result.text('check your figures');
        });
    }

    $('#TaxYear, #Income, #Deductions').on('input change', function () {
        window.clearTimeout(timer);
        timer = window.setTimeout(refresh, 250);
    });
}(jQuery));
