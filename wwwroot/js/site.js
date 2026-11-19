// Máscaras simples (sem bibliotecas). Uso: data-mask="cpf|tel|data|nome"
(function () {
    function dig(v) { return v.replace(/\D/g, ""); }

    var mascaras = {
        cpf: function (v) {
            v = dig(v).slice(0, 11);
            if (v.length > 9) return v.slice(0, 3) + "." + v.slice(3, 6) + "." + v.slice(6, 9) + "-" + v.slice(9);
            if (v.length > 6) return v.slice(0, 3) + "." + v.slice(3, 6) + "." + v.slice(6);
            if (v.length > 3) return v.slice(0, 3) + "." + v.slice(3);
            return v;
        },
        tel: function (v) {
            v = dig(v).slice(0, 11);
            if (v.length > 7) return "(" + v.slice(0, 2) + ")" + v.slice(2, 7) + "-" + v.slice(7);
            if (v.length > 2) return "(" + v.slice(0, 2) + ")" + v.slice(2);
            if (v.length > 0) return "(" + v;
            return v;
        },
        data: function (v) {
            v = dig(v).slice(0, 8);
            if (v.length > 4) return v.slice(0, 2) + "/" + v.slice(2, 4) + "/" + v.slice(4);
            if (v.length > 2) return v.slice(0, 2) + "/" + v.slice(2);
            return v;
        },
        nome: function (v) {
            return v.replace(/[0-9_!@#$%^&*()+=\[\]{};:"\\|,.<>\/?~`]/g, "");
        }
    };

    document.addEventListener("input", function (e) {
        var f = mascaras[e.target.getAttribute && e.target.getAttribute("data-mask")];
        if (f) e.target.value = f(e.target.value);
    });

    // Mostra/esconde o responsável financeiro
    var chk = document.querySelector('input[type=checkbox][name$="MesmoResponsavel"]');
    var fin = document.getElementById("financeiro");
    if (chk && fin) {
        var alterna = function () { fin.hidden = chk.checked; };
        chk.addEventListener("change", alterna);
        alterna();
    }
})();
