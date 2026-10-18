(function ($) {
    "use strict"


    // Daterange picker
    $('.input-daterange-datepicker').daterangepicker({

        format: "jYYYY/jMM/jDD",
        buttonClasses: ['btn', 'btn-sm'],
        applyClass: 'btn-danger',
        cancelClass: 'btn-inverse',

        "locale": {
            // "format": 'jYYYY/jMM/jDD',
            "separator": " - ",
            "applyLabel": "ثبت",
            "cancelLabel": "لغو",
            "customRangeLabel": "بازه دلخواه",
            "daysOfWeek": [
                "ی", "د", "س", "چ", "پ", "ج", "ش"
            ],
            "monthNames": [
                "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
            ],
            "firstDay": 1
        }
    });

    $('.input-daterange-timepicker').daterangepicker({
        timePicker: true,
        format: 'jYYYY/jM/jD h:mm A',
        timePickerIncrement: 30,
        timePicker12Hour: true,
        timePickerSeconds: false,
        buttonClasses: ['btn', 'btn-sm'],
        applyClass: 'btn-danger',
        cancelClass: 'btn-inverse',
        "locale": {
            // "format": 'jYYYY/jMM/jDD',
            "separator": " - ",
            "applyLabel": "ثبت",
            "cancelLabel": "لغو",
            "customRangeLabel": "بازه دلخواه",
            "daysOfWeek": [
                "ی", "د", "س", "چ", "پ", "ج", "ش"
            ],
            "monthNames": [
                "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
            ],
            "firstDay": 1
        }

    });
    $('.input-limit-datepicker').daterangepicker({
        "locale": {
            // "format": 'jYYYY/jMM/jDD',
            "separator": " - ",
            "applyLabel": "ثبت",
            "cancelLabel": "لغو",
            "customRangeLabel": "بازه دلخواه",
            "daysOfWeek": [
                "ی", "د", "س", "چ", "پ", "ج", "ش"
            ],
            "monthNames": [
                "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
            ],
            "firstDay": 1
        },
        format: "jYYYY/jM/jD",
        minDate: '06/01/1399',
        maxDate: '06/30/1399',
        buttonClasses: ['btn', 'btn-sm'],
        applyClass: 'btn-danger',
        cancelClass: 'btn-inverse',

        dateLimit: {
            days: 6
        },

    });
})(jQuery);