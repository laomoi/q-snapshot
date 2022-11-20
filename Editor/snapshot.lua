local luaqprof = {}

rawset(_G, 'luaqprof', luaqprof)

local weak_meta_table = {__mode = 'k', __ignore_luaqprof = true}
setmetatable(luaqprof, weak_meta_table)


-- luaqprof.test = function()
--     local a ={}
--     setmetatable(a, weak_meta_table)
--     return getmetatable(a)
-- end


luaqprof.get_metatable = function(t)
    return getmetatable(t)
end

luaqprof.get_registry = function()
    return debug.getregistry()
end

luaqprof.get_func_source = function(func)
    local info = debug.getinfo(func, 'Sl')
    local result = string.format('func:%s&line:%d', info.source, info.linedefined)
    return result
end
   
luaqprof.get_memory_usage = function()
    return math.floor(collectgarbage("count"))
end
return luaqprof
