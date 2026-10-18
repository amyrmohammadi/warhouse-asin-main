(function ($) {
    "use strict"

    // MAterial Date picker
    $('#mdate').bootstrapMaterialDatePicker({
        weekStart: 6,
        time: false
    });
    $('#timepicker').bootstrapMaterialDatePicker({
        weekStart: 6,
        format: 'HH:mm',
        time: true,
        date: false
    });
    $('#date-format').bootstrapMaterialDatePicker({
        weekStart: 6,
        format: 'dddd jDD jMMMM jYYYY - HH:mm',
    });

    $('#min-date').bootstrapMaterialDatePicker({
        weekStart: 6,
        format: 'jDD/jMM/jYYYY HH:mm',
        minDate: new Date()
    });

})(jQuery);